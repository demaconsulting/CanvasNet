using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.SpecModel;

/// <summary>
///     Unit tests of the concatenations and facets of the view tree built by <see cref="SpecNormalizer"/>: the node kinds, the layout settings, the resolve defaults of
///     each kind, the facet encoding channels of a unit view, the chart each view belongs to, and the limits.
/// </summary>
public class SpecNormalizerCompositionTests
{
    /// <summary>
    ///     Parses a specification and builds its view tree with the configuration of the specification.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="limits">The limits, or null.</param>
    /// <returns>The parsed specification and its view tree.</returns>
    private static (SpecTree Tree, LayerTree Layers) Normalize(string json, VegaLiteLimits? limits = null)
    {
        var tree = SpecParser.Parse(json, "c.json", limits);
        return (tree, SpecNormalizer.Layers(tree, limits, ConfigResolver.Resolve(tree, null)));
    }

    /// <summary>
    ///     Validates that an <c>hconcat</c> is a concatenation node whose children are its views in order, one row, with the unit views as its leaves.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_HConcatBuildsConcatNodeWithChildrenInOrder()
    {
        // Arrange / Act
        var (tree, layers) = Normalize("""{ "hconcat": [ { "mark": "bar" }, { "mark": "point" } ] }""");

        // Assert: the root, two units; the units are the view objects themselves (nothing to merge)
        Assert.Equal(ViewKind.Concat, layers.Root.Kind);
        Assert.Equal("hconcat", layers.Root.Operator);
        Assert.True(layers.Root.IsComposition);
        Assert.True(layers.HasComposition);
        Assert.Same(layers.Root, Assert.Single(layers.Compositions));
        Assert.Equal(2, layers.Root.Children.Count);
        Assert.Equal(["/hconcat/0", "/hconcat/1"], layers.Units.Select(u => u.Unit!.Pointer));
        Assert.Same(tree.Root["hconcat"]!.Items[0], layers.Units[0].Unit);
        Assert.Equal(3, layers.ViewCount);
        Assert.Equal([1, 1], layers.Units.Select(u => u.Depth));
        Assert.All(layers.Units, u => Assert.Same(layers.Root, u.Parent));
        Assert.Null(layers.Root.Unit);
        Assert.Null(layers.Root.ChartRoot);
        Assert.Null(layers.Root.Layout!.Columns);
    }

    /// <summary>
    ///     Validates that a <c>vconcat</c> has one column, and that a <c>concat</c> without <c>columns</c> has none set.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_VConcatHasOneColumn()
    {
        // Arrange / Act
        var (_, vertical) = Normalize("""{ "vconcat": [ { "mark": "bar" }, { "mark": "point" } ], "columns": 4 }""");
        var (_, wrapped) = Normalize("""{ "concat": [ { "mark": "bar" }, { "mark": "point" } ] }""");

        // Assert: the columns of a vconcat are always one, whatever is written
        Assert.Equal(1, vertical.Root.Layout!.Columns);
        Assert.Equal("vconcat", vertical.Root.Operator);
        Assert.Null(wrapped.Root.Layout!.Columns);
    }

    /// <summary>
    ///     Validates the layout defaults of a concatenation (spacing 20, each alignment, no centering, full bounds) and the layout properties of the view.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_ConcatReadsColumnsSpacingAlignCenterBounds()
    {
        // Arrange / Act
        var (_, defaults) = Normalize("""{ "concat": [ { "mark": "bar" } ] }""");
        var (_, written) = Normalize(
            """
            { "concat": [ { "mark": "bar" }, { "mark": "point" }, { "mark": "rule" } ],
              "columns": 2, "spacing": { "row": 5, "column": 7 }, "align": { "row": "none", "column": "all" },
              "center": { "row": true }, "bounds": "flush" }
            """);

        // Assert
        var standard = defaults.Root.Layout!;
        Assert.Equal((20d, 20d), (standard.SpacingRow, standard.SpacingColumn));
        Assert.Equal((CompositionAlign.Each, CompositionAlign.Each), (standard.AlignRow, standard.AlignColumn));
        Assert.False(standard.CenterRow || standard.CenterColumn || standard.Flush);
        var layout = written.Root.Layout!;
        Assert.Equal(2, layout.Columns);
        Assert.Equal((5d, 7d), (layout.SpacingRow, layout.SpacingColumn));
        Assert.Equal((CompositionAlign.None, CompositionAlign.All), (layout.AlignRow, layout.AlignColumn));
        Assert.Equal((true, false), (layout.CenterRow, layout.CenterColumn));
        Assert.True(layout.Flush);
    }

    /// <summary>
    ///     Validates that the configuration gives the spacing and the columns of a concatenation, that a spacing object gives only what it names (the rest is the
    ///     configured spacing), that one number is both, and that <c>hconcat</c> ignores the configured columns.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_ConcatSpacingObjectFallsBackToConfig()
    {
        // Arrange / Act
        var (_, object1) = Normalize("""{ "config": { "concat": { "spacing": 12, "columns": 3 } }, "concat": [ { "mark": "bar" } ], "spacing": { "row": 5 } }""");
        var (_, number) = Normalize("""{ "config": { "concat": { "spacing": 12 } }, "concat": [ { "mark": "bar" } ], "spacing": 3 }""");
        var (_, horizontal) = Normalize("""{ "config": { "concat": { "columns": 3 } }, "hconcat": [ { "mark": "bar" } ] }""");

        // Assert
        Assert.Equal((5d, 12d), (object1.Root.Layout!.SpacingRow, object1.Root.Layout.SpacingColumn));
        Assert.Equal(3, object1.Root.Layout.Columns);
        Assert.Equal((3d, 3d), (number.Root.Layout!.SpacingRow, number.Root.Layout.SpacingColumn));
        Assert.Null(horizontal.Root.Layout!.Columns);
    }

    /// <summary>
    ///     Validates the resolve defaults of a concatenation: the scales of the position channels, <c>theta</c> and <c>radius</c> are independent, every other scale
    ///     is shared, and an explicit mode wins.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_ConcatDefaultsPositionScalesIndependentAndOthersShared()
    {
        // Arrange / Act
        var (_, layers) = Normalize("""{ "hconcat": [ { "mark": "bar" } ] }""");
        var (_, explicitShared) = Normalize("""{ "hconcat": [ { "mark": "bar" } ], "resolve": { "scale": { "x": "shared", "color": "independent" } } }""");

        // Assert
        var resolve = layers.Root.Resolve;
        foreach (var channel in new[] { "x", "y", "xOffset", "yOffset", "theta", "radius" })
        {
            Assert.Equal(ResolveMode.Independent, resolve.Scale(channel));
        }

        foreach (var channel in new[] { "color", "size", "shape", "opacity", "stroke" })
        {
            Assert.Equal(ResolveMode.Shared, resolve.Scale(channel));
            Assert.Equal(ResolveMode.Shared, resolve.Legend(channel));
        }

        Assert.Equal(ResolveMode.Shared, explicitShared.Root.Resolve.Scale("x"));
        Assert.Equal(ResolveMode.Independent, explicitShared.Root.Resolve.Scale("color"));
        Assert.Equal(ResolveMode.Independent, explicitShared.Root.Resolve.Legend("color"));
        Assert.Same(LayerResolve.ConcatDefault, layers.Root.Resolve);
    }

    /// <summary>
    ///     Validates that the children of a concatenation never share an axis, even when the scale is shared or <c>resolve.axis</c> says shared.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_ConcatAxisIsAlwaysIndependent()
    {
        // Arrange / Act
        var (_, layers) = Normalize(
            """{ "hconcat": [ { "mark": "bar" } ], "resolve": { "scale": { "y": "shared" }, "axis": { "x": "shared" } } }""");

        // Assert
        Assert.Equal(ResolveMode.Independent, layers.Root.Resolve.Axis("x"));
        Assert.Equal(ResolveMode.Independent, layers.Root.Resolve.Axis("y"));
        Assert.Equal(ResolveMode.Shared, layers.Root.Resolve.Scale("y"));
    }

    /// <summary>
    ///     Validates that an explicit <c>shared</c> axis of a concatenation is reported as a warning at the entry, once for each such channel.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_ExplicitSharedConcatAxisIsReported()
    {
        // Arrange / Act
        var (_, shared) = Normalize("""{ "vconcat": [ { "mark": "bar" } ], "resolve": { "axis": { "x": "shared", "y": "independent" } } }""");
        var (_, none) = Normalize("""{ "vconcat": [ { "mark": "bar" } ], "resolve": { "axis": { "x": "independent" } } }""");

        // Assert
        var warning = Assert.Single(shared.Warnings);
        Assert.Equal("vl-concat-axis-shared", warning.Id);
        Assert.Equal(FeatureReportKind.Warning, warning.Kind);
        Assert.Equal("/resolve/axis/x", warning.Location.Pointer);
        Assert.Empty(none.Warnings);
    }

    /// <summary>
    ///     Validates that a <c>facet</c> with a field definition is a wrapped facet over its <c>spec</c>, with the wrap columns, and that the facet is a node with one child.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_FacetFieldDefIsWrapFacet()
    {
        // Arrange / Act
        var (tree, layers) = Normalize("""{ "facet": { "field": "a", "type": "nominal" }, "columns": 3, "spec": { "mark": "bar" } }""");

        // Assert
        Assert.Equal(ViewKind.Facet, layers.Root.Kind);
        Assert.Equal("facet", layers.Root.Operator);
        Assert.True(layers.Root.Facet!.IsWrap);
        Assert.Same(tree.Root["facet"], layers.Root.Facet.Wrap);
        Assert.Null(layers.Root.Facet.Row);
        Assert.Equal(3, layers.Root.Layout!.Columns);
        Assert.Equal(CompositionAlign.All, layers.Root.Layout.AlignColumn);
        var body = Assert.Single(layers.Root.Children);
        Assert.Same(tree.Root["spec"], body.Source);
        Assert.False(body.IsFacetBody);
        Assert.Equal(2, layers.ViewCount);
        Assert.Same(body, Assert.Single(layers.Units));
        Assert.Same(body, body.ChartRoot);
    }

    /// <summary>
    ///     Validates that a <c>facet</c> of a <c>row</c> and a <c>column</c> field has no wrap columns: the <c>columns</c> of the view is dropped with a warning.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_FacetRowColumnMappingKeepsColumnsOnlyForWrap()
    {
        // Arrange / Act
        var (tree, layers) = Normalize(
            """{ "facet": { "row": { "field": "a" }, "column": { "field": "b" } }, "columns": 2, "spec": { "mark": "bar" } }""");

        // Assert
        Assert.False(layers.Root.Facet!.IsWrap);
        Assert.Same(tree.Root["facet"]!["row"], layers.Root.Facet.Row);
        Assert.Same(tree.Root["facet"]!["column"], layers.Root.Facet.Column);
        Assert.Null(layers.Root.Layout!.Columns);
        var warning = Assert.Single(layers.Warnings);
        Assert.Equal("vl-composition-columns-ignored", warning.Id);
        Assert.Equal("/columns", warning.Location.Pointer);
    }

    /// <summary>
    ///     Validates the resolve defaults of a facet: everything is shared except the <c>theta</c> scale, and an independent scale makes its axis independent.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_FacetDefaultsSharedExceptTheta()
    {
        // Arrange / Act
        var (_, layers) = Normalize("""{ "facet": { "field": "a" }, "spec": { "mark": "bar" } }""");
        var (_, independent) = Normalize(
            """{ "facet": { "field": "a" }, "spec": { "mark": "bar" }, "resolve": { "scale": { "x": "independent" } } }""");

        // Assert
        var resolve = layers.Root.Resolve;
        Assert.Equal(ResolveMode.Shared, resolve.Scale("x"));
        Assert.Equal(ResolveMode.Shared, resolve.Axis("x"));
        Assert.Equal(ResolveMode.Shared, resolve.Scale("color"));
        Assert.Equal(ResolveMode.Shared, resolve.Legend("color"));
        Assert.Equal(ResolveMode.Independent, resolve.Scale("theta"));
        Assert.Same(LayerResolve.FacetDefault, resolve);
        Assert.Equal(ResolveMode.Independent, independent.Root.Resolve.Scale("x"));
        Assert.Equal(ResolveMode.Independent, independent.Root.Resolve.Axis("x"));
    }

    /// <summary>
    ///     Validates that a unit view with <c>row</c> and <c>column</c> channels is a facet over a synthetic chart that has its mark, its size and its encoding without those channels.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_UnitWithRowColumnChannelsBecomesFacetWithBody()
    {
        // Arrange / Act
        var (tree, layers) = Normalize(
            """
            { "mark": "bar", "width": 100,
              "encoding": { "x": { "field": "a" }, "row": { "field": "r" }, "column": { "field": "c" } } }
            """);

        // Assert: the facet is the view itself, faceted by the two channels
        Assert.Equal(ViewKind.Facet, layers.Root.Kind);
        Assert.Same(tree.Root, layers.Root.Source);
        Assert.Same(tree.Root["encoding"]!["row"], layers.Root.Facet!.Row);
        Assert.Same(tree.Root["encoding"]!["column"], layers.Root.Facet.Column);
        Assert.Null(layers.Root.Layout!.Columns);

        // Assert: the body is a chart over the same view, whose unit has the mark, the size and the encoding without the faceting channels
        var body = Assert.Single(layers.Root.Children);
        Assert.True(body.IsFacetBody);
        Assert.Same(body, Assert.Single(layers.Units));
        Assert.Same(body, body.ChartRoot);
        Assert.Equal(2, layers.ViewCount);
        Assert.Equal(["x"], body.Unit!["encoding"]!.Properties.Select(p => p.Name));
        Assert.Equal("bar", body.Unit["mark"]!.StringValue);
        Assert.Equal(100, body.Unit["width"]!.NumberValue);
        Assert.Equal(string.Empty, body.Unit.Pointer);
        Assert.Same(tree.Root["encoding"]!["x"], body.Unit["encoding"]!["x"]);
    }

    /// <summary>
    ///     Validates that the data, the transform and the title of a faceted unit view belong to the facet: the body reads none of them.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_FacetBodyDoesNotReapplyDataTransformOrTitle()
    {
        // Arrange / Act
        var (tree, layers) = Normalize(
            """
            { "data": { "values": [ { "a": 1 } ] }, "transform": [ { "filter": "datum.a > 0" } ], "title": "T", "mark": "bar",
              "encoding": { "x": { "field": "a" }, "column": { "field": "a" } } }
            """);

        // Assert
        var body = layers.Root.Children[0];
        Assert.Same(tree.Root["data"], layers.Root.Data);
        Assert.Same(tree.Root["transform"], layers.Root.Transform);
        Assert.Null(body.Data);
        Assert.Null(body.Transform);
        Assert.Null(body.Unit!["data"]);
        Assert.Null(body.Unit["transform"]);
        Assert.Null(body.Unit["title"]);
    }

    /// <summary>
    ///     Validates that a <c>row</c> or <c>column</c> channel wins over a <c>facet</c> channel.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_RowWinsOverFacetChannel()
    {
        // Arrange / Act
        var (tree, layers) = Normalize(
            """{ "mark": "bar", "encoding": { "x": { "field": "a" }, "facet": { "field": "f" }, "row": { "field": "r" } } }""");

        // Assert
        Assert.False(layers.Root.Facet!.IsWrap);
        Assert.Same(tree.Root["encoding"]!["row"], layers.Root.Facet.Row);
        Assert.Null(layers.Root.Facet.Column);
        Assert.Equal(["x"], layers.Units[0].Unit!["encoding"]!.Properties.Select(p => p.Name));
    }

    /// <summary>
    ///     Validates that a <c>facet</c> channel is a wrapped facet whose <c>columns</c>, <c>spacing</c> and <c>align</c> are those of the channel definition, over the configuration.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_FacetChannelIsWrapWithItsOwnLayout()
    {
        // Arrange / Act
        var (_, layers) = Normalize(
            """
            { "config": { "facet": { "columns": 9, "spacing": 11 } }, "mark": "bar",
              "encoding": { "facet": { "field": "f", "columns": 4, "align": "none" } } }
            """);
        var (_, fromConfig) = Normalize("""{ "config": { "facet": { "columns": 9 } }, "mark": "bar", "encoding": { "facet": { "field": "f" } } }""");

        // Assert
        Assert.True(layers.Root.Facet!.IsWrap);
        var layout = layers.Root.Layout!;
        Assert.Equal(4, layout.Columns);
        Assert.Equal((CompositionAlign.None, CompositionAlign.None), (layout.AlignRow, layout.AlignColumn));
        Assert.Equal((11d, 11d), (layout.SpacingRow, layout.SpacingColumn));
        Assert.Equal(9, fromConfig.Root.Layout!.Columns);
    }

    /// <summary>
    ///     Validates that a unit view in a layer is not faceted: its <c>row</c> channel stays in the encoding of the layer.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_LayerChildWithRowChannelIsNotFaceted()
    {
        // Arrange / Act
        var (_, layers) = Normalize("""{ "layer": [ { "mark": "bar", "encoding": { "x": { "field": "a" }, "row": { "field": "r" } } } ] }""");

        // Assert
        Assert.Equal(ViewKind.Layer, layers.Root.Kind);
        Assert.False(layers.HasComposition);
        Assert.Equal(["x", "row"], layers.Units[0].Unit!["encoding"]!.Properties.Select(p => p.Name));
    }

    /// <summary>
    ///     Validates that a unit view in a concatenation is faceted by its channels, and that a unit view in the <c>spec</c> of a facet is not.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_UnitInConcatIsFacetedButNotInFacetSpec()
    {
        // Arrange / Act
        var (_, concat) = Normalize("""{ "hconcat": [ { "mark": "bar", "encoding": { "column": { "field": "c" } } } ] }""");
        var (_, facet) = Normalize("""{ "facet": { "field": "f" }, "spec": { "mark": "bar", "encoding": { "column": { "field": "c" } } } }""");

        // Assert
        var cell = Assert.Single(concat.Root.Children);
        Assert.Equal(ViewKind.Facet, cell.Kind);
        Assert.Single(cell.Children);
        Assert.True(cell.Children[0].IsFacetBody);
        Assert.Equal(ViewKind.Unit, facet.Root.Children[0].Kind);
        Assert.Equal(["column"], facet.Units[0].Unit!["encoding"]!.Properties.Select(p => p.Name));
    }

    /// <summary>
    ///     Validates that a <c>spec</c> of a facet that is itself a composition is a located specification error.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_FacetSpecThatIsACompositionIsSpecError()
    {
        // Arrange / Act
        var ex = Assert.Throws<VegaLiteSpecException>(
            () => Normalize("""{ "facet": { "field": "a" }, "spec": { "hconcat": [ { "mark": "bar" } ] } }"""));

        // Assert
        Assert.Equal("/spec", ex.Location.Pointer);
        Assert.Contains("hconcat", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a view of a <c>layer</c> that is a composition is a located specification error.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_LayerChildThatIsACompositionIsSpecError()
    {
        // Arrange / Act
        var ex = Assert.Throws<VegaLiteSpecException>(
            () => Normalize("""{ "layer": [ { "mark": "bar" }, { "hconcat": [ { "mark": "point" } ] } ] }"""));

        // Assert
        Assert.Equal("/layer/1", ex.Location.Pointer);
    }

    /// <summary>
    ///     Validates that the size of a unit view comes from its chart (the child of the concatenation), not from the root of the tree, and that a layer's own size is ignored.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_ChartRootSizeComesFromTheChartNotTheTreeRoot()
    {
        // Arrange / Act
        var (tree, layers) = Normalize(
            """
            { "width": 5, "hconcat": [
                { "width": 50, "layer": [ { "mark": "bar", "width": 999 } ] },
                { "width": 70, "mark": "point" } ] }
            """);

        // Assert
        var inLayer = layers.Units[0];
        Assert.Same(layers.Root.Children[0], inLayer.ChartRoot);
        Assert.Equal(50, inLayer.Unit!["width"]!.NumberValue);
        Assert.Same(tree.Root["hconcat"]!.Items[0]["width"], inLayer.Unit["width"]);
        Assert.Equal(70, layers.Units[1].Unit!["width"]!.NumberValue);
        Assert.Same(layers.Units[1], layers.Units[1].ChartRoot);
        Assert.True(inLayer.InComposition);
        Assert.False(layers.Root.InComposition);
    }

    /// <summary>
    ///     Validates that a concatenation hands no encoding down: a <c>concat</c> with an <c>encoding</c> does not give it to its children.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_ConcatHandsNoEncodingDown()
    {
        // Arrange / Act
        var (_, layers) = Normalize(
            """{ "encoding": { "x": { "field": "a" } }, "hconcat": [ { "layer": [ { "mark": "bar", "encoding": { "y": { "field": "b" } } } ] } ] }""");

        // Assert
        Assert.Equal(["y"], layers.Units[0].Unit!["encoding"]!.Properties.Select(p => p.Name));
    }

    /// <summary>
    ///     Validates that every nesting level of concatenations, facets and layers counts towards <see cref="VegaLiteLimits.MaxCompositionDepth"/>, and that exactly that depth is accepted.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_NestedCompositionsCountTowardsMaxCompositionDepth()
    {
        // Arrange
        const string Concat = """{ "vconcat": [ { "hconcat": [ { "mark": "bar" } ] } ] }""";
        const string Facet = """{ "facet": { "field": "a" }, "spec": { "layer": [ { "mark": "bar" } ] } }""";

        // Act
        var (_, accepted) = Normalize(Concat, new VegaLiteLimits { MaxCompositionDepth = 2 });
        var concatEx = Assert.Throws<VegaLiteRenderException>(() => Normalize(Concat, new VegaLiteLimits { MaxCompositionDepth = 1 }));
        var (_, facetAccepted) = Normalize(Facet, new VegaLiteLimits { MaxCompositionDepth = 2 });
        var facetEx = Assert.Throws<VegaLiteRenderException>(() => Normalize(Facet, new VegaLiteLimits { MaxCompositionDepth = 1 }));

        // Assert: the inner concatenation, and the layer in the facet, are the levels that exceed a limit of one
        Assert.Single(accepted.Units);
        Assert.Equal("/vconcat/0/hconcat", concatEx.Location.Pointer);
        Assert.Contains("MaxCompositionDepth", concatEx.Message, StringComparison.Ordinal);
        Assert.Single(facetAccepted.Units);
        Assert.Equal("/spec/layer", facetEx.Location.Pointer);
    }

    /// <summary>
    ///     Validates that the views of a concatenation count towards <see cref="VegaLiteLimits.MaxViews"/>, and that exactly that many are accepted.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_ConcatChildrenCountTowardsMaxViews()
    {
        // Arrange: the root and three children are four views
        const string Json = """{ "hconcat": [ { "mark": "bar" }, { "mark": "line" }, { "mark": "point" } ] }""";

        // Act
        var (_, accepted) = Normalize(Json, new VegaLiteLimits { MaxViews = 4 });
        var ex = Assert.Throws<VegaLiteRenderException>(() => Normalize(Json, new VegaLiteLimits { MaxViews = 3 }));

        // Assert
        Assert.Equal(3, accepted.Units.Count);
        Assert.Contains("MaxViews", ex.Message, StringComparison.Ordinal);
        Assert.StartsWith("/hconcat/", ex.Location.Pointer, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a faceted unit view counts its facet and its body as two views.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_FacetedUnitCountsFacetAndBody()
    {
        // Arrange
        const string Json = """{ "mark": "bar", "encoding": { "row": { "field": "r" } } }""";

        // Act
        var (_, accepted) = Normalize(Json, new VegaLiteLimits { MaxViews = 2 });
        var ex = Assert.Throws<VegaLiteRenderException>(() => Normalize(Json, new VegaLiteLimits { MaxViews = 1 }));

        // Assert
        Assert.Equal(2, accepted.ViewCount);
        Assert.Equal(string.Empty, ex.Location.Pointer);
    }

    /// <summary>
    ///     Validates that a composite mark in a concatenation is expanded into its layers, which belong to the chart of the composite mark.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_CompositeMarkInsideConcatIsExpanded()
    {
        // Arrange
        var tree = SpecParser.Parse(
            """
            { "hconcat": [ { "data": { "values": [ { "a": "x", "b": 1 } ] }, "mark": "boxplot",
                "encoding": { "x": { "field": "a", "type": "nominal" }, "y": { "field": "b", "type": "quantitative" } } } ] }
            """);

        // Act
        var layers = SpecNormalizer.Expand(tree, ConfigResolver.Resolve(tree, null)).Layers;

        // Assert
        var composite = Assert.Single(layers.Root.Children);
        Assert.Equal(ViewKind.Expansion, composite.Kind);
        Assert.True(composite.Children.Count > 1);
        Assert.True(layers.Units.Count >= composite.Children.Count);
        Assert.All(layers.Units, u => Assert.Same(composite, u.ChartRoot));
        Assert.All(layers.Units, u => Assert.True(u.InComposition));
    }

    /// <summary>
    ///     Validates that a layered view inside a facet <c>spec</c> is the chart of its layers and that the facet's resolve is the one the layers' groups climb to.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_LayersInFacetSpecShareTheChart()
    {
        // Arrange / Act
        var (_, layers) = Normalize(
            """{ "facet": { "field": "f" }, "spec": { "layer": [ { "mark": "bar" }, { "mark": "rule" } ] } }""");

        // Assert
        var chart = Assert.Single(layers.Root.Children);
        Assert.Equal(ViewKind.Layer, chart.Kind);
        Assert.Same(chart, chart.ChartRoot);
        Assert.All(layers.Units, u => Assert.Same(chart, u.ChartRoot));
        Assert.Equal([2, 2], layers.Units.Select(u => u.Depth));
        Assert.Equal(4, layers.ViewCount);
    }

    /// <summary>
    ///     Validates that the configuration the tree is built with is optional: the defaults apply (spacing 20).
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_DefaultConfigGivesSpacingTwenty()
    {
        // Arrange
        var tree = SpecParser.Parse("""{ "facet": { "field": "f" }, "spec": { "mark": "bar" } }""");

        // Act
        var layers = SpecNormalizer.Layers(tree);

        // Assert
        Assert.Equal((20d, 20d), (layers.Root.Layout!.SpacingRow, layers.Root.Layout.SpacingColumn));
    }
}
