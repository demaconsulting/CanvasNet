using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.SpecModel;

/// <summary>
///     Unit tests of the data-dependent part of the view tree: the instantiation of the cells of a facet (<see cref="LayerTree.InstantiateFacet"/>, <see cref="LayerTree.Refresh"/>,
///     <see cref="LayerTree.FacetViews"/>) and the grouping of the views by the data they load (<see cref="LayerTree.DataSites"/>).
/// </summary>
public class SpecNormalizerFacetCellTests
{
    /// <summary>The body of the facets of most tests: one point view.</summary>
    private const string Point = """{ "mark": "point", "encoding": { "x": { "field": "v", "type": "quantitative" } } }""";

    /// <summary>
    ///     Builds the view tree of a specification.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <returns>The parsed specification and its view tree.</returns>
    private static (SpecTree Tree, LayerTree Layers) Normalize(string json)
    {
        var tree = SpecParser.Parse(json, "c.json");
        return (tree, SpecNormalizer.Layers(tree, null, ConfigResolver.Resolve(tree, null)));
    }

    /// <summary>
    ///     Makes the cells of a facet with one column for each text.
    /// </summary>
    /// <param name="texts">The column values.</param>
    /// <returns>The cells.</returns>
    private static FacetCellInfo[] Cells(params string[] texts) =>
        [.. texts.Select((text, i) => new FacetCellInfo(i, 0, i, default, DataValue.FromString(text), default))];

    /// <summary>
    ///     Validates that the template of a facet (one view) is replaced by a copy for each cell: the copies hold the same view and unit objects, have the facet as their parent and the
    ///     cell on every node, are their own charts, and the tree is refreshed (units and view count).
    /// </summary>
    [Fact]
    public void LayerTree_InstantiateFacet_ReplacesTheTemplateWithOneCopyPerCell()
    {
        // Arrange: the facet, its body: two views
        var (_, layers) = Normalize("""{ "data": { "values": [] }, "facet": { "column": { "field": "c", "type": "nominal" } }, "spec": """ + Point + " }");
        var facet = layers.Root;
        var template = facet.Children[0];
        var cells = Cells("a", "b", "c");
        Assert.Equal(2, layers.ViewCount);
        Assert.False(facet.HasCells);

        // Act
        layers.InstantiateFacet(facet, cells);

        // Assert: three copies replace the template; the facet and three views are four
        Assert.True(facet.HasCells);
        Assert.Equal(3, facet.Children.Count);
        Assert.Equal(4, layers.ViewCount);
        Assert.Equal(facet.Children, layers.Units);
        Assert.All(facet.Children, c => Assert.Same(facet, c.Parent));
        Assert.Equal([0, 1, 2], facet.Children.Select(c => c.Cell!.Index));
        Assert.Equal(cells, facet.Children.Select(c => c.Cell!));

        // Assert: the copies share the specification objects and are their own charts
        Assert.All(facet.Children, c =>
        {
            Assert.NotSame(template, c);
            Assert.Same(template.Source, c.Source);
            Assert.Same(template.Unit, c.Unit);
            Assert.Same(c, c.ChartRoot);
            Assert.True(c.IsUnit);
            Assert.Equal(template.Depth, c.Depth);
        });
        Assert.Null(template.Cell);
    }

    /// <summary>
    ///     Validates that a layered body is copied with its layers: the copies of the layers belong to the copy of the layered view (their chart), and the units of the tree are in
    ///     cell order.
    /// </summary>
    [Fact]
    public void LayerTree_InstantiateFacet_LayeredBodyCopiesEachLayerAndMapsChartRoots()
    {
        // Arrange: the facet, the layered view and its two layers
        var (_, layers) = Normalize(
            """
            { "data": { "values": [] }, "facet": { "field": "c", "type": "nominal" },
              "spec": { "layer": [ { "mark": "point", "encoding": { "x": { "field": "v", "type": "quantitative" } } }, { "mark": "line" } ] } }
            """);
        var facet = layers.Root;
        var originals = layers.Units.ToList();
        Assert.Equal(4, layers.ViewCount);
        Assert.Equal((3, 1), layers.FacetViews(facet));

        // Act
        layers.InstantiateFacet(facet, Cells("a", "b"));

        // Assert: two cells of three views and the facet
        Assert.Equal(7, layers.ViewCount);
        Assert.Equal(2, facet.Children.Count);
        Assert.All(facet.Children, c => Assert.Equal(ViewKind.Layer, c.Kind));
        Assert.Equal([0, 0, 1, 1], layers.Units.Select(u => u.Cell!.Index));
        Assert.Equal([2, 2, 2, 2], layers.Units.Select(u => u.Depth));

        // Assert: each leaf is a child of the copy of the layered view of its cell, which is its chart; none is an original
        Assert.All(layers.Units, u =>
        {
            Assert.Same(u.Parent, u.ChartRoot);
            Assert.Contains(u.Parent, facet.Children);
            Assert.DoesNotContain(u, originals);
        });
        Assert.Same(layers.Units[0].Unit, originals[0].Unit);
        Assert.Same(layers.Units[2].Unit, originals[0].Unit);
        Assert.NotSame(layers.Units[0], layers.Units[2]);

        // Assert: the resolve of the layered view is kept
        Assert.Same(originals[0].Parent!.Resolve, facet.Children[0].Resolve);
    }

    /// <summary>
    ///     Validates that the synthetic body of a unit view with faceting channels is copied as a body: its data and transform stay with the facet.
    /// </summary>
    [Fact]
    public void LayerTree_InstantiateFacet_FacetedUnitCopiesTheSyntheticBody()
    {
        // Arrange: a unit view with a column channel is a facet over a body
        var (_, layers) = Normalize(
            """
            { "data": { "values": [] }, "transform": [ { "filter": "datum.v > 0" } ], "mark": "point",
              "encoding": { "x": { "field": "v", "type": "quantitative" }, "column": { "field": "c", "type": "nominal" } } }
            """);
        var body = layers.Units[0];

        // Act
        layers.InstantiateFacet(layers.Root, Cells("a", "b"));

        // Assert
        Assert.Equal(2, layers.Units.Count);
        Assert.All(layers.Units, u =>
        {
            Assert.True(u.IsFacetBody);
            Assert.Null(u.Data);
            Assert.Null(u.Transform);
            Assert.Same(body.Unit, u.Unit);
        });
        Assert.Equal(3, layers.ViewCount);
    }

    /// <summary>
    ///     Validates the count of views that the limit is checked with: the views of one cell and the views that are not in the cells, before and after the cells exist.
    /// </summary>
    [Fact]
    public void LayerTree_FacetViews_CountsTheViewsOfOneCellAndTheOthers()
    {
        // Arrange: the concatenation, the facet and its body, and a bar: four views
        var (_, layers) = Normalize("""{ "hconcat": [ { "facet": { "row": { "field": "r", "type": "nominal" } }, "spec": """ + Point + """ }, { "mark": "bar" } ] }""");
        var facet = layers.Compositions.Single(c => c.Kind == ViewKind.Facet);
        Assert.Equal(4, layers.ViewCount);

        // Act / Assert: before: one view in a cell, three outside
        Assert.Equal((1, 3), layers.FacetViews(facet));

        // Act: two cells
        layers.InstantiateFacet(facet, Cells("x", "y"));

        // Assert: five views; a cell is still one view, the other three are the concatenation, the facet and the bar
        Assert.Equal(5, layers.ViewCount);
        Assert.Equal((1, 3), layers.FacetViews(facet));

        // Act / Assert: no cells at all
        var (_, none) = Normalize("""{ "facet": { "row": { "field": "r", "type": "nominal" } }, "spec": """ + Point + " }");
        none.InstantiateFacet(none.Root, []);
        Assert.Equal((0, 1), none.FacetViews(none.Root));
    }

    /// <summary>
    ///     Validates that a facet without cells has no children, no units and only itself as a view, and that a facet is instantiated once only and nothing else is a facet.
    /// </summary>
    [Fact]
    public void LayerTree_InstantiateFacet_RefusesTwiceAndNonFacetsAndAllowsNoCells()
    {
        // Arrange
        var (_, layers) = Normalize("""{ "facet": { "field": "c", "type": "nominal" }, "spec": """ + Point + " }");
        var (_, concat) = Normalize("""{ "hconcat": [ { "mark": "bar" } ] }""");

        // Act
        layers.InstantiateFacet(layers.Root, []);

        // Assert: no cell, no unit; the facet is still a composition
        Assert.True(layers.Root.HasCells);
        Assert.Empty(layers.Root.Children);
        Assert.Empty(layers.Units);
        Assert.Equal(1, layers.ViewCount);
        Assert.Same(layers.Root, Assert.Single(layers.Compositions));
        Assert.Throws<InvalidOperationException>(() => layers.InstantiateFacet(layers.Root, Cells("a")));
        Assert.Throws<ArgumentException>(() => concat.InstantiateFacet(concat.Root, Cells("a")));
        Assert.Throws<ArgumentException>(() => concat.FacetViews(concat.Root));
        Assert.Throws<ArgumentNullException>(() => layers.InstantiateFacet(null!, []));
        Assert.Throws<ArgumentNullException>(() => layers.InstantiateFacet(layers.Root, null!));
    }

    /// <summary>
    ///     Validates that the copies of a repeat share the data object they declare: one site for all of them, apart from the site of the root that has no data.
    /// </summary>
    [Fact]
    public void LayerTree_DataSites_CopiesOfARepeatShareOneSite()
    {
        // Arrange: three copies of one view that has its own data and reads the repeated field as a date
        var (_, layers) = Normalize(
            """
            { "repeat": [ "a", "b", "c" ],
              "spec": { "data": { "values": [ { "a": 1, "b": 2, "c": 3 } ] }, "mark": "point",
                        "encoding": { "x": { "field": { "repeat": "repeat" }, "type": "temporal" } } } }
            """);

        // Act
        var sites = layers.DataSites();

        // Assert: the root without data, then the data of the copies
        Assert.Equal(2, sites.Count);
        Assert.Null(sites[0].Data);
        Assert.Equal([layers.Root], sites[0].Nodes);
        Assert.Equal(3, sites[1].Nodes.Count);
        Assert.Equal(layers.Units, sites[1].Nodes);
        Assert.Same(sites[1].Data, layers.Units[0].Data);

        // Assert: the views of the site hold the fields each copy reads (replaced by the repeat), so one load parses all three as dates
        Assert.Equal(["a", "b", "c"], TemporalFields.CollectAll(sites[1].Views).OrderBy(f => f, StringComparer.Ordinal));
    }

    /// <summary>
    ///     Validates that the cells of a facet and the views that inherit the data of the root are one site: the root's, with the facet among its views, whether or not the cells exist.
    /// </summary>
    [Fact]
    public void LayerTree_DataSites_FacetCellsShareTheRootSiteAndItsFacetFieldsAreCollected()
    {
        // Arrange
        var (_, layers) = Normalize(
            """{ "data": { "values": [ { "d": "2024-01-02", "v": 1 } ] }, "facet": { "field": "d", "type": "temporal" }, "spec": """ + Point + " }");

        // Act
        var before = layers.DataSites();
        layers.InstantiateFacet(layers.Root, Cells("a", "b"));
        var after = layers.DataSites();

        // Assert: one site, the root's, with the facet and the body's unit
        var site = Assert.Single(before);
        Assert.Same(layers.Root.Source["data"], site.Data);
        Assert.Same(layers.Root.Facet, Assert.Single(site.Facets));
        Assert.Equal(["d"], TemporalFields.CollectAll(site.Views, site.Facets).OrderBy(f => f, StringComparer.Ordinal));

        // Assert: the cells are views of that site; the views are the same objects once each
        var again = Assert.Single(after);
        Assert.Equal(site.Views, again.Views);
        Assert.Equal([layers.Root], again.Nodes);
    }

    /// <summary>
    ///     Validates that a view with its own data is a site of its own and that the views below it read that table, while the siblings read the root's; and that a view that is not
    ///     needed is left out.
    /// </summary>
    [Fact]
    public void LayerTree_DataSites_ViewWithOwnDataIsItsOwnSiteAndNeededFiltersViews()
    {
        // Arrange: the root has data; the second child has its own and a layer below it
        var (tree, layers) = Normalize(
            """
            { "data": { "values": [ { "v": 1 } ] },
              "hconcat": [ { "mark": "bar" },
                           { "data": { "values": [ { "v": 2 } ] }, "layer": [ { "mark": "point" }, { "mark": "line" } ] } ] }
            """);
        var second = layers.Root.Children[1];

        // Act
        var all = layers.DataSites();
        var firstOnly = layers.DataSites(new HashSet<LayerNode> { layers.Root, layers.Root.Children[0] });

        // Assert: two sites; the root's holds the root and the first child, the other the second child and its layers
        Assert.Equal(2, all.Count);
        Assert.Same(tree.Root["data"], all[0].Data);
        Assert.Same(second.Source["data"], all[1].Data);
        Assert.Equal([layers.Root], all[0].Nodes);
        Assert.Equal([second], all[1].Nodes);
        Assert.Same(tree.Root, all[0].Views[0]);
        Assert.Contains(layers.Root.Children[0].Source, all[0].Views);
        Assert.DoesNotContain(second.Source, all[0].Views);
        Assert.Contains(second.Source, all[1].Views);
        Assert.All(second.Children, c =>
        {
            Assert.Contains(c.Source, all[1].Views);
            Assert.Contains(c.Unit!, all[1].Views);
        });

        // Assert: without the second child its site is not there
        Assert.Single(firstOnly);
        Assert.Same(tree.Root["data"], firstOnly[0].Data);
    }
}
