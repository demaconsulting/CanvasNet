using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.SpecModel;

/// <summary>
///     Unit tests of the expansion of <c>repeat</c> by <see cref="SpecNormalizer"/>: the concatenations and layered views it becomes, the copies and their repeated values,
///     the replacement of the <c>{"repeat": ...}</c> field references, and the limits that are checked before the copies are made.
/// </summary>
public class SpecNormalizerRepeatTests
{
    /// <summary>
    ///     Parses a specification and builds its view tree.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="limits">The limits, or null.</param>
    /// <returns>The parsed specification and its view tree.</returns>
    private static (SpecTree Tree, LayerTree Layers) Normalize(string json, VegaLiteLimits? limits = null)
    {
        var tree = SpecParser.Parse(json, "r.json", limits);
        return (tree, SpecNormalizer.Layers(tree, limits));
    }

    /// <summary>
    ///     Validates that a <c>repeat</c> over an array is a concatenation of one copy of the <c>spec</c> for each value, with the columns and the repeated value of each copy,
    ///     and that the reference of the field is replaced by a string located at the reference.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_ArrayRepeatMakesConcatWithColumns()
    {
        // Arrange / Act
        var (tree, layers) = Normalize(
            """
            { "repeat": [ "a", "b", "c" ], "columns": 2,
              "spec": { "mark": "point", "encoding": { "x": { "field": { "repeat": "repeat" }, "type": "quantitative" } } } }
            """);

        // Assert
        Assert.Equal(ViewKind.Concat, layers.Root.Kind);
        Assert.Equal("repeat", layers.Root.Operator);
        Assert.Equal(2, layers.Root.Layout!.Columns);
        Assert.Equal(CompositionAlign.All, layers.Root.Layout.AlignRow);
        Assert.Equal(3, layers.Root.Children.Count);
        Assert.Equal(4, layers.ViewCount);
        Assert.Equal(["a", "b", "c"], layers.Units.Select(u => u.Repeater!.Repeat));
        Assert.Equal(["a", "b", "c"], layers.Units.Select(u => u.Unit!["encoding"]!["x"]!["field"]!.StringValue));
        Assert.All(layers.Units, u => Assert.Equal("/spec/encoding/x/field", u.Unit!["encoding"]!["x"]!["field"]!.Pointer));
        Assert.All(layers.Units, u => Assert.Same(tree.Root["spec"], u.Source));
        Assert.Equal("quantitative", layers.Units[0].Unit!["encoding"]!["x"]!["type"]!.StringValue);
        Assert.True(layers.HasComposition);
    }

    /// <summary>
    ///     Validates that a <c>repeat</c> of rows and columns is the row-major cross product (rows outer) with the columns of the <c>column</c> list.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_RowColumnRepeatIsRowMajorCrossProduct()
    {
        // Arrange / Act
        var (_, layers) = Normalize(
            """{ "repeat": { "row": [ "r1", "r2" ], "column": [ "c1", "c2", "c3" ] }, "spec": { "mark": "point" } }""");

        // Assert
        Assert.Equal(6, layers.Root.Children.Count);
        Assert.Equal(7, layers.ViewCount);
        Assert.Equal(
            ["r1/c1", "r1/c2", "r1/c3", "r2/c1", "r2/c2", "r2/c3"],
            layers.Units.Select(u => u.Repeater!.Row + "/" + u.Repeater.Column));
        Assert.Equal(3, layers.Root.Layout!.Columns);
        Assert.Equal(CompositionAlign.All, layers.Root.Layout.AlignColumn);
    }

    /// <summary>
    ///     Validates that a <c>repeat</c> of rows only has one column, and one of columns only has as many columns as values.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_RowOnlyRepeatHasOneColumn()
    {
        // Arrange / Act
        var (_, rows) = Normalize("""{ "repeat": { "row": [ "a", "b" ] }, "spec": { "mark": "point" } }""");
        var (_, columns) = Normalize("""{ "repeat": { "column": [ "a", "b", "c" ] }, "spec": { "mark": "point" } }""");

        // Assert
        Assert.Equal(1, rows.Root.Layout!.Columns);
        Assert.Equal(["a", "b"], rows.Units.Select(u => u.Repeater!.Row));
        Assert.All(rows.Units, u => Assert.Null(u.Repeater!.Column));
        Assert.Equal(3, columns.Root.Layout!.Columns);
        Assert.Equal(["a", "b", "c"], columns.Units.Select(u => u.Repeater!.Column));
    }

    /// <summary>
    ///     Validates that a <c>repeat</c> of layers is a layered view whose layers are the copies of the <c>spec</c>, sharing one chart, with the layer value in each.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_LayerRepeatMakesLayerNode()
    {
        // Arrange / Act
        var (_, layers) = Normalize(
            """
            { "repeat": { "layer": [ "a", "b" ] },
              "spec": { "mark": "line", "encoding": { "y": { "field": { "repeat": "layer" }, "type": "quantitative" } } } }
            """);

        // Assert: a layered view, not a composition; the copies are its layers and have its chart
        Assert.Equal(ViewKind.Layer, layers.Root.Kind);
        Assert.False(layers.Root.IsComposition);
        Assert.Empty(layers.Compositions);
        Assert.Equal(["a", "b"], layers.Units.Select(u => u.Repeater!.Layer));
        Assert.Equal(["a", "b"], layers.Units.Select(u => u.Unit!["encoding"]!["y"]!["field"]!.StringValue));
        Assert.All(layers.Units, u => Assert.Same(layers.Root, u.ChartRoot));
        Assert.Null(layers.Root.Repeater);
        Assert.Equal(3, layers.ViewCount);
        Assert.True(layers.HasComposition);
    }

    /// <summary>
    ///     Validates that a <c>repeat</c> of rows and layers is a repeat of rows whose every cell is a layered view of the layers, each copy knowing its row and layer.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_RowAndLayerRepeatNestsLayers()
    {
        // Arrange / Act
        var (_, layers) = Normalize(
            """{ "repeat": { "row": [ "r1", "r2" ], "layer": [ "a", "b" ] }, "spec": { "mark": "line" } }""");

        // Assert: the root, two layered cells, four units
        Assert.Equal(ViewKind.Concat, layers.Root.Kind);
        Assert.Equal(1, layers.Root.Layout!.Columns);
        Assert.Equal([ViewKind.Layer, ViewKind.Layer], layers.Root.Children.Select(c => c.Kind));
        Assert.Equal(7, layers.ViewCount);
        Assert.Equal(["r1/a", "r1/b", "r2/a", "r2/b"], layers.Units.Select(u => u.Repeater!.Row + "/" + u.Repeater.Layer));
        Assert.Equal([2, 2, 2, 2], layers.Units.Select(u => u.Depth));
        Assert.Same(layers.Root.Children[0], layers.Units[1].ChartRoot);
        Assert.Same(layers.Root.Children[1], layers.Units[2].ChartRoot);
    }

    /// <summary>
    ///     Validates that the references in a channel's <c>field</c> and <c>datum</c>, in the <c>field</c> of a <c>sort</c> and in the <c>field</c> of a condition are replaced
    ///     by the repeated value, keeping everything else of the definition.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_RepeatReplacesRefInFieldSortFieldDatumAndCondition()
    {
        // Arrange / Act
        var (_, layers) = Normalize(
            """
            { "repeat": { "row": [ "q" ] }, "spec": { "mark": "point", "encoding": {
                "x": { "field": { "repeat": "row" }, "type": "quantitative", "sort": { "field": { "repeat": "row" }, "op": "mean" } },
                "y": { "datum": { "repeat": "row" } },
                "color": { "condition": { "test": "datum.a > 1", "field": { "repeat": "row" }, "type": "quantitative" }, "value": "gray" } } } }
            """);

        // Assert
        var encoding = layers.Units[0].Unit!["encoding"]!;
        Assert.Equal("q", encoding["x"]!["field"]!.StringValue);
        Assert.Equal("q", encoding["x"]!["sort"]!["field"]!.StringValue);
        Assert.Equal("mean", encoding["x"]!["sort"]!["op"]!.StringValue);
        Assert.Equal("q", encoding["y"]!["datum"]!.StringValue);
        Assert.Equal("q", encoding["color"]!["condition"]!["field"]!.StringValue);
        Assert.Equal("datum.a > 1", encoding["color"]!["condition"]!["test"]!.StringValue);
        Assert.Equal("gray", encoding["color"]!["value"]!.StringValue);
        Assert.Empty(layers.Warnings);
    }

    /// <summary>
    ///     Validates that a reference the repeats do not define drops the channel and is reported once (at the reference), however many copies have it.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_MissingRepeatValueDropsChannelWithWarning()
    {
        // Arrange / Act
        var (_, layers) = Normalize(
            """
            { "repeat": [ "a", "b" ], "spec": { "mark": "point", "encoding": {
                "x": { "field": { "repeat": "row" }, "type": "quantitative" }, "y": { "field": "b" } } } }
            """);

        // Assert
        Assert.All(layers.Units, u => Assert.Equal(["y"], u.Unit!["encoding"]!.Properties.Select(p => p.Name)));
        var warning = Assert.Single(layers.Warnings);
        Assert.Equal("vl-repeat-ref-unresolved", warning.Id);
        Assert.Equal(FeatureReportKind.Warning, warning.Kind);
        Assert.Equal("/spec/encoding/x/field", warning.Location.Pointer);
    }

    /// <summary>
    ///     Validates that an unresolved reference in a <c>sort</c> drops only the sort, and one in a condition drops only that condition.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_MissingRepeatValueInSortOrConditionDropsOnlyThat()
    {
        // Arrange / Act
        var (_, layers) = Normalize(
            """
            { "repeat": [ "a" ], "spec": { "mark": "point", "encoding": {
                "x": { "field": "f", "sort": { "field": { "repeat": "column" }, "op": "mean" } },
                "color": { "condition": [ { "test": "datum.a > 1", "field": { "repeat": "layer" } }, { "test": "datum.a > 2", "value": "red" } ], "value": "gray" } } } }
            """);

        // Assert
        var encoding = layers.Units[0].Unit!["encoding"]!;
        Assert.Equal("f", encoding["x"]!["field"]!.StringValue);
        Assert.Null(encoding["x"]!["sort"]);
        var conditions = encoding["color"]!["condition"]!.Items;
        Assert.Equal("datum.a > 2", Assert.Single(conditions)["test"]!.StringValue);
        Assert.Equal(["/spec/encoding/x/sort/field", "/spec/encoding/color/condition/0/field"], layers.Warnings.Select(w => w.Location.Pointer));
    }

    /// <summary>
    ///     Validates that <c>columns</c> next to rows and columns is dropped with a warning at <c>columns</c>.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_RepeatColumnsWithMappingIsIgnoredWithWarning()
    {
        // Arrange / Act
        var (_, layers) = Normalize("""{ "repeat": { "column": [ "a", "b" ] }, "columns": 5, "spec": { "mark": "point" } }""");

        // Assert
        Assert.Equal(2, layers.Root.Layout!.Columns);
        var warning = Assert.Single(layers.Warnings);
        Assert.Equal("vl-composition-columns-ignored", warning.Id);
        Assert.Equal("/columns", warning.Location.Pointer);
    }

    /// <summary>
    ///     Validates that the <c>data</c> of a repeat stays on its concatenation (the copies inherit it) and that a <c>data</c> of the repeated view is the copies' own.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_RepeatDataStaysOnTheConcatAndChildDataWins()
    {
        // Arrange / Act
        var (tree, layers) = Normalize(
            """
            { "data": { "values": [ { "a": 1 } ] }, "repeat": [ "a", "b" ],
              "spec": { "data": { "values": [ { "a": 2 } ] }, "mark": "bar" } }
            """);
        var (_, inherited) = Normalize("""{ "data": { "values": [ { "a": 1 } ] }, "repeat": [ "a", "b" ], "spec": { "mark": "bar" } }""");

        // Assert
        Assert.Same(tree.Root["data"], layers.Root.Data);
        Assert.All(layers.Units, u => Assert.Same(tree.Root["spec"]!["data"], u.Data));
        Assert.All(inherited.Units, u => Assert.Null(u.Data));
    }

    /// <summary>
    ///     Validates that the copies keep the original nodes: a copy with nothing to replace is the repeated view object itself, so a diagnostic points at the text of the <c>spec</c>.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_RepeatCopiesKeepOriginalPointers()
    {
        // Arrange / Act
        var (tree, layers) = Normalize("""{ "repeat": [ "a", "b" ], "spec": { "mark": "bar", "encoding": { "x": { "field": "f" } } } }""");

        // Assert
        Assert.All(layers.Units, u => Assert.Same(tree.Root["spec"], u.Unit));
        Assert.All(layers.Units, u => Assert.Equal("/spec", u.Unit!.Pointer));
        Assert.Equal(["a", "b"], layers.Units.Select(u => u.Repeater!.Repeat));
    }

    /// <summary>
    ///     Validates that the number of copies of a repeat is checked against <see cref="VegaLiteLimits.MaxViews"/> before any copy is made: the product of rows and columns, and of the
    ///     layers of each cell, even when it is far larger than the limit.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_RepeatProductIsCheckedBeforeExpansion()
    {
        // Arrange: 3 x 4 cells and the root are 13 views
        const string Json = """{ "repeat": { "row": [ "a", "b", "c" ], "column": [ "a", "b", "c", "d" ] }, "spec": { "mark": "bar" } }""";
        var values = string.Join(",", Enumerable.Range(0, 1000).Select(i => "\"f" + i + "\""));
        var huge = "{ \"repeat\": { \"row\": [" + values + "], \"column\": [" + values + "] }, \"spec\": { \"mark\": \"bar\" } }";
        const string WithLayers = """{ "repeat": { "row": [ "a", "b" ], "layer": [ "x", "y", "z" ] }, "spec": { "mark": "bar" } }""";

        // Act
        var (_, accepted) = Normalize(Json, new VegaLiteLimits { MaxViews = 13 });
        var ex = Assert.Throws<VegaLiteRenderException>(() => Normalize(Json, new VegaLiteLimits { MaxViews = 12 }));
        var hugeEx = Assert.Throws<VegaLiteRenderException>(() => Normalize(huge));
        var layersEx = Assert.Throws<VegaLiteRenderException>(() => Normalize(WithLayers, new VegaLiteLimits { MaxViews = 8 }));

        // Assert: nothing near a million copies was made, the error is at the repeat
        Assert.Equal(13, accepted.ViewCount);
        Assert.Equal("/repeat", ex.Location.Pointer);
        Assert.Contains("MaxViews", ex.Message, StringComparison.Ordinal);
        Assert.Contains("1000000", hugeEx.Message, StringComparison.Ordinal);
        Assert.Equal("/repeat", layersEx.Location.Pointer);
    }

    /// <summary>
    ///     Validates that a <c>repeat</c> inside a <c>repeat</c> counts as two levels, and that the inner value overrides the outer one of the same name.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_NestedRepeatCountsLevelsAndInnerValueWins()
    {
        // Arrange
        const string Json = """{ "repeat": [ "a" ], "spec": { "repeat": [ "b" ], "spec": { "mark": "bar" } } }""";

        // Act
        var (_, accepted) = Normalize(Json, new VegaLiteLimits { MaxCompositionDepth = 2 });
        var ex = Assert.Throws<VegaLiteRenderException>(() => Normalize(Json, new VegaLiteLimits { MaxCompositionDepth = 1 }));

        // Assert
        Assert.Equal("b", Assert.Single(accepted.Units).Repeater!.Repeat);
        Assert.Equal("/spec/repeat", ex.Location.Pointer);
    }

    /// <summary>
    ///     Validates that a unit view with a faceting channel inside a repeat is a facet of each copy, whose field reference is replaced.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_RepeatOfFacetedUnitFacetsByTheRepeatedField()
    {
        // Arrange / Act
        var (_, layers) = Normalize(
            """
            { "repeat": [ "a", "b" ], "spec": { "mark": "bar",
                "encoding": { "x": { "field": "x" }, "row": { "field": { "repeat": "repeat" } } } } }
            """);

        // Assert
        Assert.Equal([ViewKind.Facet, ViewKind.Facet], layers.Root.Children.Select(c => c.Kind));
        Assert.Equal(["a", "b"], layers.Root.Children.Select(c => c.Facet!.Row!["field"]!.StringValue));
        Assert.Equal(2, layers.Units.Count);
        Assert.All(layers.Units, u => Assert.True(u.IsFacetBody));
    }

    /// <summary>
    ///     Validates that an empty <c>repeat</c> list makes a concatenation with no children and no units.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_EmptyRepeatHasNoViews()
    {
        // Arrange / Act
        var (_, layers) = Normalize("""{ "repeat": [], "spec": { "mark": "bar" } }""");

        // Assert
        Assert.Equal(ViewKind.Concat, layers.Root.Kind);
        Assert.Empty(layers.Root.Children);
        Assert.Empty(layers.Units);
        Assert.Equal(1, layers.ViewCount);
    }
}
