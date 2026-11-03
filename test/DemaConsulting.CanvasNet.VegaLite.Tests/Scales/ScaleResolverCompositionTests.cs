using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Scales;

/// <summary>
///     Tests of the scales across the views of a composition: the children of a concatenation (independent positions, shared colors, <c>resolve</c>), the cells of a facet (shared by
///     default, independent by <c>resolve</c>, <c>theta</c> independent), the step sizes of one chart (<see cref="LayeredScales.HintsFor"/>), the message of a conflict, and the
///     union of explicit domains, with numbers derived by hand from the rows of each test.
/// </summary>
public class ScaleResolverCompositionTests
{
    /// <summary>The data of most tests: categories b, a, c, a with q 3, 7, 12, 5 and r 10, 20, 35, 15 (as in the layered tests).</summary>
    private const string SampleData = """
        "data": { "values": [
          { "k": "b", "q": 3, "r": 10 },
          { "k": "a", "q": 7, "r": 20 },
          { "k": "c", "q": 12, "r": 35 },
          { "k": "a", "q": 5, "r": 15 }
        ] }
        """;

    /// <summary>The data of the facet tests: cell p has the categories a and b with v 2 and 4, cell q has the category c with v 10.</summary>
    private const string FacetData = """
        "data": { "values": [
          { "g": "p", "k": "a", "v": 2 },
          { "g": "p", "k": "b", "v": 4 },
          { "g": "q", "k": "c", "v": 10 }
        ] }
        """;

    /// <summary>A bar of the sample data: <c>k</c> on x and the field on y.</summary>
    private const string BarTemplate = """{ "mark": "bar", "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "FIELD", "type": "quantitative" } } }""";

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Resolves the scales of the unit views of a composition: each reads the table of the nearest data above it, or its facet cell.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <returns>The scales and the view tree (its facet instantiated).</returns>
    private static (LayeredScales Scales, LayerTree Layers) Resolve(string json)
    {
        var tree = SpecParser.Parse(json);
        var config = ConfigResolver.Resolve(tree, null);
        var layers = SpecNormalizer.Layers(tree, null, config);
        var shared = DataLoader.Load(tree.Root["data"], tree.SourceName, null, Ct);

        // The cells of the facet get the rows of their value
        var facet = layers.Compositions.SingleOrDefault(c => c.Kind == ViewKind.Facet);
        var cellTables = new List<DataTable>();
        if (facet is not null)
        {
            var (perCell, outside) = layers.FacetViews(facet);
            var context = Context(tree);
            var partition = EncodingTransforms.Facet(facet.Facet!, shared, perCell, outside, context);
            layers.InstantiateFacet(facet, partition.Infos);
            cellTables.AddRange(partition.Cells.Select(c => c.Table));
        }

        var inputs = new List<LayerScaleInput>();
        foreach (var node in layers.Units)
        {
            var table = node.Cell is { } cell ? cellTables[cell.Index] : TableOf(node, shared, tree);
            var plan = EncodingPlan.Create(node.Unit!, config);
            inputs.Add(new LayerScaleInput(node, plan, EncodingTransforms.ApplyStandalone(plan, table, node.Unit!, null, null, Ct)));
        }

        return (ScaleResolver.ResolveLayers(inputs, config, null, null, Ct), layers);
    }

    /// <summary>
    ///     Finds the table of a view: the one of the nearest <c>data</c> above it.
    /// </summary>
    /// <param name="node">The view.</param>
    /// <param name="shared">The table of the root.</param>
    /// <param name="tree">The specification.</param>
    /// <returns>The table.</returns>
    private static DataTable TableOf(LayerNode node, DataTable shared, SpecTree tree)
    {
        for (var current = node; current is not null; current = current.Parent)
        {
            if (current.Data is { } data && !ReferenceEquals(current, tree.Root))
            {
                return DataLoader.Load(data, tree.SourceName, null, Ct);
            }
        }

        return shared;
    }

    /// <summary>
    ///     Builds the context that the facet partition needs.
    /// </summary>
    /// <param name="tree">The specification.</param>
    /// <returns>The context.</returns>
    private static TransformContext Context(SpecTree tree)
    {
        var limits = VegaLiteLimits.Default;
        var parameters = new Dictionary<string, DataValue>(StringComparer.Ordinal);
        var evaluator = new ExpressionEvaluator(limits, Ct, parameters);
        return new TransformContext(tree, new DataLoader(tree.SourceName, limits, cancellationToken: Ct), evaluator, parameters, limits, Ct, []);
    }

    /// <summary>
    ///     Builds a concatenation over the sample data.
    /// </summary>
    /// <param name="top">More top-level properties, each with a trailing comma, or empty.</param>
    /// <param name="children">The views (the text of the elements of the <c>hconcat</c> array).</param>
    /// <returns>The specification text.</returns>
    private static string HConcat(string top, params string[] children) =>
        "{" + top + SampleData + ", \"hconcat\": [" + string.Join(", ", children) + "]}";

    /// <summary>
    ///     Builds a facet specification by <c>g</c>, with a bar whose y is <c>v</c>.
    /// </summary>
    /// <param name="top">More top-level properties, each with a trailing comma, or empty.</param>
    /// <param name="mark">The mark and encoding of the body, without the braces.</param>
    /// <returns>The specification text.</returns>
    private static string Facet(string top, string mark) =>
        "{" + top + FacetData + ", \"facet\": { \"column\": { \"field\": \"g\", \"type\": \"nominal\" } }, \"spec\": {" + mark + "}}";

    /// <summary>
    ///     Makes a bar of the sample data over a field.
    /// </summary>
    /// <param name="field">The field on y.</param>
    /// <returns>The view text.</returns>
    private static string Bar(string field) => BarTemplate.Replace("FIELD", field, StringComparison.Ordinal);

    /// <summary>
    ///     Creates a string category.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The value.</returns>
    private static DataValue S(string text) => DataValue.FromString(text);

    /// <summary>
    ///     Validates that the children of a concatenation keep their own position scales: x and y are independent by default, so each child has the domain of its own data, there is no
    ///     scale of the whole view, and every child is a group of its own.
    /// </summary>
    [Fact]
    public void ScaleResolver_ResolveLayers_ConcatChildrenKeepIndependentPositionScales()
    {
        // Arrange / Act: q reaches 12 and r reaches 35; a bar's domain starts at zero
        var (result, _) = Resolve(HConcat(string.Empty, Bar("q"), Bar("r")));

        // Assert
        Assert.Null(result.Merged["y"]);
        Assert.Null(result.Merged["x"]);
        Assert.Equal([[0], [1]], result.Groups["y"]);
        Assert.Equal([[0], [1]], result.Groups["x"]);
        Assert.Equal((0.0, 12.0), (result.PerLayer[0]["y"]!.DomainStart, result.PerLayer[0]["y"]!.DomainEnd));
        Assert.Equal((0.0, 35.0), (result.PerLayer[1]["y"]!.DomainStart, result.PerLayer[1]["y"]!.DomainEnd));
        Assert.Equal(("q", "r"), (result.PerLayer[0]["y"]!.Title, result.PerLayer[1]["y"]!.Title));
    }

    /// <summary>
    ///     Validates that the children of a concatenation share the scales of the other channels: the color of both is one ordinal scale with the union of the categories of both
    ///     children (a, b, c and the d of the second child's own data), bound to each child's field.
    /// </summary>
    [Fact]
    public void ScaleResolver_ResolveLayers_ConcatChildrenShareColorAndUnionItsDomain()
    {
        // Arrange / Act
        var (result, _) = Resolve(HConcat(
            string.Empty,
            """{ "mark": "point", "encoding": { "x": { "field": "q", "type": "quantitative" }, "color": { "field": "k", "type": "nominal" } } }""",
            """{ "data": { "values": [ { "k": "d", "q": 1 }, { "k": "a", "q": 2 } ] }, "mark": "point", "encoding": { "x": { "field": "q", "type": "quantitative" }, "color": { "field": "k", "type": "nominal" } } }"""));

        // Assert: one color scale for the whole view over a, b, c, d; the position scales stay apart
        var expected = new[] { S("a"), S("b"), S("c"), S("d") };
        Assert.Equal([[0, 1]], result.Groups["color"]);
        Assert.Equal(expected, result.Merged["color"]!.DiscreteDomain);
        Assert.Equal(expected, result.PerLayer[0]["color"]!.DiscreteDomain);
        Assert.Equal(expected, result.PerLayer[1]["color"]!.DiscreteDomain);
        Assert.Equal([[0], [1]], result.Groups["x"]);
        Assert.Null(result.Merged["x"]);
    }

    /// <summary>
    ///     Validates that <c>resolve.scale.y: "shared"</c> on a concatenation merges the y domains of its children (0 to 12 and 0 to 35 give 0 to 35) while x stays independent.
    /// </summary>
    [Fact]
    public void ScaleResolver_ResolveLayers_ConcatChildrenResolveSharedXMergesDomains()
    {
        // Arrange / Act
        var (result, _) = Resolve(HConcat("\"resolve\": { \"scale\": { \"y\": \"shared\" } },", Bar("q"), Bar("r")));

        // Assert
        Assert.Equal([[0, 1]], result.Groups["y"]);
        Assert.Equal((0.0, 35.0), (result.Merged["y"]!.DomainStart, result.Merged["y"]!.DomainEnd));
        Assert.Equal((0.0, 35.0), (result.PerLayer[0]["y"]!.DomainStart, result.PerLayer[0]["y"]!.DomainEnd));
        Assert.Equal("q, r", result.Merged["y"]!.Title);
        Assert.Equal([[0], [1]], result.Groups["x"]);
    }

    /// <summary>
    ///     Validates that the cells of a facet share every scale by default: the two cells have the categories a, b of cell p and c of cell q as one x scale, and the y domain 0 to 10.
    /// </summary>
    [Fact]
    public void ScaleResolver_ResolveLayers_FacetCellsShareAllScalesByDefault()
    {
        // Arrange / Act
        var (result, layers) = Resolve(Facet(string.Empty, """ "mark": "bar", "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } } """));

        // Assert: two cells
        Assert.Equal(2, layers.Units.Count);
        Assert.Equal([[0, 1]], result.Groups["x"]);
        Assert.Equal([[0, 1]], result.Groups["y"]);
        Assert.Equal([S("a"), S("b"), S("c")], result.PerLayer[0]["x"]!.DiscreteDomain);
        Assert.Equal([S("a"), S("b"), S("c")], result.PerLayer[1]["x"]!.DiscreteDomain);
        Assert.Equal((0.0, 10.0), (result.PerLayer[0]["y"]!.DomainStart, result.PerLayer[0]["y"]!.DomainEnd));
        Assert.Equal((0.0, 10.0), (result.PerLayer[1]["y"]!.DomainStart, result.PerLayer[1]["y"]!.DomainEnd));
        Assert.NotNull(result.Merged["y"]);
    }

    /// <summary>
    ///     Validates that <c>resolve.scale.y: "independent"</c> gives each cell the domain of its own rows (0 to 4 in cell p, 0 to 10 in cell q) while x stays shared.
    /// </summary>
    [Fact]
    public void ScaleResolver_ResolveLayers_FacetIndependentScaleGivesEachCellItsOwn()
    {
        // Arrange / Act
        var (result, _) = Resolve(Facet(
            "\"resolve\": { \"scale\": { \"y\": \"independent\" } },",
            """ "mark": "bar", "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } } """));

        // Assert
        Assert.Equal([[0], [1]], result.Groups["y"]);
        Assert.Null(result.Merged["y"]);
        Assert.Equal((0.0, 4.0), (result.PerLayer[0]["y"]!.DomainStart, result.PerLayer[0]["y"]!.DomainEnd));
        Assert.Equal((0.0, 10.0), (result.PerLayer[1]["y"]!.DomainStart, result.PerLayer[1]["y"]!.DomainEnd));
        Assert.Equal([[0, 1]], result.Groups["x"]);
    }

    /// <summary>
    ///     Validates that the <c>theta</c> scale of a facet is independent by default (each cell of a pie has its own total) while the other channels are shared.
    /// </summary>
    [Fact]
    public void ScaleResolver_ResolveLayers_FacetThetaIsIndependentByDefault()
    {
        // Arrange / Act
        var (result, _) = Resolve(Facet(
            string.Empty,
            """ "mark": "arc", "encoding": { "theta": { "field": "v", "type": "quantitative" }, "color": { "field": "k", "type": "nominal" } } """));

        // Assert: the sums of p (2 + 4) and q (10) are not combined; the color is
        Assert.Equal([[0], [1]], result.Groups["theta"]);
        Assert.Equal([[0, 1]], result.Groups["color"]);
        Assert.Null(result.Merged["theta"]);
    }

    /// <summary>
    ///     Validates the sizes of one chart: with two charts in a concatenation (x independent) the first has three categories (60 pixels at the default step of 20, as the band of a bar is
    ///     three steps for three categories) and the second two (40); a chart that has no discrete y gets none.
    /// </summary>
    [Fact]
    public void ScaleResolver_ResolveLayers_HintsForGivesTheStepExtentOfTheChartsOwnGroup()
    {
        // Arrange / Act: the first chart reads a, b, c; the second its own data d, a
        var (result, _) = Resolve(HConcat(
            string.Empty,
            Bar("q"),
            """{ "data": { "values": [ { "k": "d", "q": 1 }, { "k": "a", "q": 2 } ] }, "mark": "bar", "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "q", "type": "quantitative" } } }"""));

        // Act
        var first = result.HintsFor([0]);
        var second = result.HintsFor([1]);
        var both = result.HintsFor([0, 1]);

        // Assert: bandspace(n, 0.1, 0.05) = n - 0.1 + 0.1 = n steps of 20
        Assert.Equal(60.0, first.DiscreteWidth!.Value, 9);
        Assert.Equal(40.0, second.DiscreteWidth!.Value, 9);
        Assert.Null(first.DiscreteHeight);
        Assert.Null(second.DiscreteHeight);

        // Assert: two charts that use two scales have no common size; nothing is a container size
        Assert.Null(both.DiscreteWidth);
        Assert.Null(first.ContainerWidth);
        Assert.Null(result.HintsFor([]).DiscreteWidth);
    }

    /// <summary>
    ///     Validates that the cells of a facet that share a step scale are all as wide as the union of the categories of all the cells (a, b, c: 60 pixels), and that independent x
    ///     scales size each cell by its own categories (a, b: 40; c: 20).
    /// </summary>
    [Fact]
    public void ScaleResolver_ResolveLayers_SharedStepAppliesToTheUnionOfAllCellCategories()
    {
        // Arrange
        const string Body = """ "mark": "bar", "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } } """;

        // Act
        var (shared, _) = Resolve(Facet(string.Empty, Body));
        var (independent, _) = Resolve(Facet("\"resolve\": { \"scale\": { \"x\": \"independent\" } },", Body));

        // Assert: both cells have three categories
        Assert.Equal(60.0, shared.HintsFor([0]).DiscreteWidth!.Value, 9);
        Assert.Equal(60.0, shared.HintsFor([1]).DiscreteWidth!.Value, 9);

        // Assert: cell p has a and b, cell q has c
        Assert.Equal(40.0, independent.HintsFor([0]).DiscreteWidth!.Value, 9);
        Assert.Equal(20.0, independent.HintsFor([1]).DiscreteWidth!.Value, 9);
    }

    /// <summary>
    ///     Validates the arguments of <see cref="LayeredScales.HintsFor"/>.
    /// </summary>
    [Fact]
    public void ScaleResolver_ResolveLayers_HintsForChecksItsArguments()
    {
        // Arrange
        var (result, _) = Resolve(HConcat(string.Empty, Bar("q"), Bar("r")));

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => result.HintsFor(null!));
        Assert.Throws<ArgumentOutOfRangeException>(() => result.HintsFor([2]));
        Assert.Throws<ArgumentOutOfRangeException>(() => result.HintsFor([-1]));
    }

    /// <summary>
    ///     Validates that the warning for scales that cannot be merged names the view, not the layer, when the views are children of a concatenation.
    /// </summary>
    [Fact]
    public void ScaleResolver_ResolveLayers_ConflictMessageNamesTheView()
    {
        // Arrange / Act: y shared, a linear scale of q and a logarithmic one of r
        var (result, _) = Resolve(HConcat(
            "\"resolve\": { \"scale\": { \"y\": \"shared\" } },",
            """{ "mark": "point", "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "q", "type": "quantitative" } } }""",
            """{ "mark": "point", "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "r", "type": "quantitative", "scale": { "type": "log" } } } }"""));

        // Assert
        var warning = Assert.Single(result.Warnings);
        Assert.Equal("vl-layer-scale-conflict", warning.Id);
        Assert.Contains("this view", warning.Message, StringComparison.Ordinal);
        Assert.Contains("another view", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("layer", warning.Message, StringComparison.Ordinal);
        Assert.Equal("/hconcat/1/encoding/y", warning.Location.Pointer);
    }

    /// <summary>
    ///     Validates that explicit domains of the children of a concatenation are combined as the shared scale of a layered view combines them: the extent of the union for a continuous
    ///     scale (0 to 100 and 50 to 200 give 0 to 200, the child without a domain ignored), every category once in order of first appearance for a discrete scale (c, b, z and b, a, y
    ///     give c, b, z, a, y), implicit categories ignored.
    /// </summary>
    [Fact]
    public void ScaleResolver_ResolveLayers_ExplicitDomainsOfConcatChildrenAreCombined()
    {
        // Arrange
        static string Child(string yDomain, string colorDomain) =>
            "{ \"mark\": \"point\", \"encoding\": { \"x\": { \"field\": \"k\", \"type\": \"nominal\" }, \"y\": { \"field\": \"q\", \"type\": \"quantitative\", \"scale\": { \"domain\": "
            + yDomain + " } }, \"color\": { \"field\": \"k\", \"type\": \"nominal\", \"scale\": { \"domain\": " + colorDomain + " } } } }";
        const string Implicit = """{ "mark": "point", "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "r", "type": "quantitative" }, "color": { "field": "k", "type": "nominal" } } }""";

        // Act
        var (result, _) = Resolve(HConcat(
            "\"resolve\": { \"scale\": { \"y\": \"shared\" } },",
            Child("[0, 100]", "[\"c\", \"b\", \"z\"]"),
            Implicit,
            Child("[50, 200]", "[\"b\", \"a\", \"y\"]")));

        // Assert: the extent 0 to 200 for every child, the implicit child between them included
        foreach (var scale in new[] { result.PerLayer[0]["y"]!, result.PerLayer[1]["y"]!, result.PerLayer[2]["y"]!, result.Merged["y"]! })
        {
            Assert.Equal((0.0, 200.0), (scale.DomainStart, scale.DomainEnd));
        }

        // Assert: c, b, z, then the new a and y; the implicit categories (a, b, c of k) add nothing
        var expected = new[] { S("c"), S("b"), S("z"), S("a"), S("y") };
        Assert.Equal(expected, result.Merged["color"]!.DiscreteDomain);
        Assert.Equal(expected, result.PerLayer[1]["color"]!.DiscreteDomain);
    }
}
