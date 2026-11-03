using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.SpecModel;

/// <summary>
///     Unit tests of <see cref="SpecNormalizer"/> and <see cref="LayerResolve"/>: the layer tree, the inheritance of data, transform, encoding and size, the
///     <c>resolve</c> rules, the limits and the overlay nodes the merged views are built from.
/// </summary>
public class SpecNormalizerTests
{
    /// <summary>
    ///     Parses a specification and builds its layer tree.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="limits">The limits, or null.</param>
    /// <returns>The parsed specification and its layer tree.</returns>
    private static (SpecTree Tree, LayerTree Layers) Normalize(string json, VegaLiteLimits? limits = null)
    {
        var tree = SpecParser.Parse(json, "n.json", limits);
        return (tree, SpecNormalizer.Layers(tree, limits));
    }

    /// <summary>
    ///     Validates that a specification without composition is one unit view that is the root object itself, so a single view takes its usual path.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_SingleViewIsTheRootItself()
    {
        // Arrange / Act
        var (tree, layers) = Normalize("""{ "mark": "point", "encoding": { "x": { "field": "a" } } }""");

        // Assert: one unit view, no overlay, nothing undrawn
        var unit = Assert.Single(layers.Units);
        Assert.Same(layers.Root, unit);
        Assert.Same(tree.Root, unit.Unit);
        Assert.False(unit.IsLayer);
        Assert.False(layers.HasComposition);
        Assert.Equal(1, layers.ViewCount);
    }

    /// <summary>
    ///     Validates that the layers are the unit views in document order, including layers of layers, and that the parent links and depths follow the nesting.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_LayersAreUnitsInDocumentOrder()
    {
        // Arrange / Act
        var (_, layers) = Normalize(
            """
            { "layer": [
                { "mark": "bar" },
                { "layer": [ { "mark": "line" }, { "mark": "point" } ] },
                { "mark": "rule" } ] }
            """);

        // Assert
        Assert.Equal(["/layer/0", "/layer/1/layer/0", "/layer/1/layer/1", "/layer/2"], layers.Units.Select(u => u.Unit!.Pointer));
        Assert.Equal(["bar", "line", "point", "rule"], layers.Units.Select(u => u.Unit!["mark"]!.StringValue));
        Assert.Equal(6, layers.ViewCount);
        Assert.True(layers.Root.IsLayer);
        Assert.Null(layers.Root.Unit);
        Assert.Equal([1, 2, 2, 1], layers.Units.Select(u => u.Depth));
        Assert.Same(layers.Root, layers.Units[0].Parent);
        Assert.Same(layers.Root.Children[1], layers.Units[1].Parent);
        Assert.Equal(3, layers.Root.Children.Count);
    }

    /// <summary>
    ///     Validates that a layer reads the encoding of the layered views above it: parent channels keep their place, a layer's channel replaces the parent's in
    ///     place, new channels follow, and every value is the original node with its own pointer.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_EncodingIsMergedInPlace()
    {
        // Arrange / Act
        var (tree, layers) = Normalize(
            """
            { "encoding": { "x": { "field": "a" }, "y": { "field": "b" }, "color": { "field": "c" } },
              "layer": [
                { "mark": "line", "encoding": { "y": { "field": "z" }, "size": { "field": "s" } } },
                { "mark": "point" } ] }
            """);

        // Assert: the first layer has x, then its own y (in the parent's position), color, then the new size
        var merged = layers.Units[0].Unit!["encoding"]!;
        Assert.Equal(["x", "y", "color", "size"], merged.Properties.Select(p => p.Name));
        Assert.Equal("z", merged["y"]!["field"]!.StringValue);
        Assert.Equal("/layer/0/encoding/y", merged["y"]!.Pointer);
        Assert.Equal("/encoding/x", merged["x"]!.Pointer);
        Assert.Same(tree.Root["encoding"]!["x"], merged["x"]);

        // Assert: a layer without an encoding reads the parent's encoding as it is (no overlay)
        Assert.Same(tree.Root["encoding"], layers.Units[1].Unit!["encoding"]);
    }

    /// <summary>
    ///     Validates the encoding of a layer inside a layer: the outermost encoding first, then each level's channels replacing or adding.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_NestedLayersMergeEveryLevel()
    {
        // Arrange / Act
        var (_, layers) = Normalize(
            """
            { "encoding": { "x": { "field": "a" }, "y": { "field": "b" } },
              "layer": [ { "encoding": { "y": { "field": "m" }, "color": { "field": "c" } },
                           "layer": [ { "mark": "bar", "encoding": { "x": { "field": "n" } } } ] } ] }
            """);

        // Assert
        var merged = layers.Units[0].Unit!["encoding"]!;
        Assert.Equal(["x", "y", "color"], merged.Properties.Select(p => p.Name));
        Assert.Equal(
            ["/layer/0/layer/0/encoding/x", "/layer/0/encoding/y", "/layer/0/encoding/color"],
            merged.Properties.Select(p => p.Pointer));
    }

    /// <summary>
    ///     Validates that a layer takes the width and the height of the top-level view and ignores its own, and that the merged view reads like the layer
    ///     (the same pointer and position) while its original properties keep theirs.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_UnitTakesSizeOfTopLevelView()
    {
        // Arrange / Act
        var (tree, layers) = Normalize(
            """
            { "width": 300, "height": { "step": 12 },
              "layer": [ { "width": 50, "height": 60, "mark": "bar" } ] }
            """);

        // Assert
        var unit = layers.Units[0].Unit!;
        Assert.Equal(300, unit["width"]!.NumberValue);
        Assert.Equal(12, unit["height"]!["step"]!.NumberValue);
        Assert.Same(tree.Root["width"], unit["width"]);
        Assert.Equal("/layer/0", unit.Pointer);
        Assert.Equal(tree.Root["layer"]!.Items[0].Line, unit.Line);
        Assert.Equal("/layer/0/mark", unit["mark"]!.Pointer);
        Assert.Same(tree.Root["layer"]!.Items[0], unit.Parent!.Items[0]);
    }

    /// <summary>
    ///     Validates that the data and the transform of each view are its own (the pipeline applies the layered view's transform first), and that a layer
    ///     without them has none.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_DataAndTransformAreEachViewsOwn()
    {
        // Arrange / Act
        var (tree, layers) = Normalize(
            """
            { "data": { "values": [ { "a": 1 } ] },
              "transform": [ { "calculate": "datum.a * 2", "as": "b" } ],
              "layer": [
                { "mark": "bar", "transform": [ { "filter": "datum.b > 1" } ] },
                { "data": { "values": [ { "a": 2 } ] }, "mark": "rule" } ] }
            """);

        // Assert
        Assert.Same(tree.Root["data"], layers.Root.Data);
        Assert.Same(tree.Root["transform"], layers.Root.Transform);
        Assert.Null(layers.Units[0].Data);
        Assert.Same(tree.Root["layer"]!.Items[0]["transform"], layers.Units[0].Transform);
        Assert.Same(tree.Root["layer"]!.Items[1]["data"], layers.Units[1].Data);
        Assert.Null(layers.Units[1].Transform);
    }

    /// <summary>
    ///     Validates that a layer whose <c>data</c> is absent or an explicit <c>null</c> has no data of its own and so inherits the data above it, as in
    ///     Vega-Lite's <c>parseRoot</c> (<c>model.data || !model.parent</c> creates a source; <c>null</c> is falsy for a child view), while a layer with a data object has it.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_NullDataOfALayerInheritsLikeAbsentData()
    {
        // Arrange / Act
        var (tree, layers) = Normalize(
            """
            { "data": { "values": [ { "a": 1 } ] },
              "layer": [
                { "mark": "bar" },
                { "data": null, "mark": "rule" },
                { "data": { "values": [ { "a": 2 } ] }, "mark": "point" } ] }
            """);

        // Assert
        Assert.Null(layers.Units[0].Data);
        Assert.Null(layers.Units[1].Data);
        Assert.Same(tree.Root["layer"]!.Items[2]["data"], layers.Units[2].Data);
    }

    /// <summary>
    ///     Validates the <c>resolve</c> of a layered view: everything is shared by default, an independent scale makes its guides independent, and
    ///     an explicit guide mode applies to a shared scale.
    /// </summary>
    [Fact]
    public void LayerResolve_Read_FollowsScaleForGuides()
    {
        // Arrange / Act
        var (_, layers) = Normalize(
            """
            { "resolve": { "scale": { "y": "independent" }, "axis": { "x": "independent" }, "legend": { "color": "independent", "size": "shared" } },
              "layer": [ { "mark": "bar" }, { "mark": "line" } ] }
            """);
        var resolve = layers.Root.Resolve;

        // Assert
        Assert.Equal(ResolveMode.Independent, resolve.Scale("y"));
        Assert.Equal(ResolveMode.Shared, resolve.Scale("x"));
        Assert.Equal(ResolveMode.Independent, resolve.Axis("y"));
        Assert.Equal(ResolveMode.Independent, resolve.Axis("x"));
        Assert.Equal(ResolveMode.Shared, resolve.Scale("color"));
        Assert.Equal(ResolveMode.Independent, resolve.Legend("color"));
        Assert.Equal(ResolveMode.Shared, resolve.Legend("size"));
        Assert.Equal(ResolveMode.Shared, resolve.Legend("shape"));
        Assert.Same(LayerResolve.Default, LayerResolve.Read(null));
        Assert.Equal(ResolveMode.Shared, LayerResolve.Default.Axis("x"));
    }

    /// <summary>
    ///     Validates that nesting deeper than <see cref="VegaLiteLimits.MaxCompositionDepth"/> is a located render error and exactly that depth is accepted.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_DepthLimitIsEnforced()
    {
        // Arrange
        const string Json = """{ "layer": [ { "layer": [ { "layer": [ { "mark": "bar" } ] } ] } ] }""";
        var accepted = new VegaLiteLimits { MaxCompositionDepth = 3 };
        var rejected = new VegaLiteLimits { MaxCompositionDepth = 2 };

        // Act
        var (_, layers) = Normalize(Json, accepted);
        var tree = SpecParser.Parse(Json, "d.json");
        var ex = Assert.Throws<VegaLiteRenderException>(() => SpecNormalizer.Layers(tree, rejected));

        // Assert: the third layer level is the one that exceeds the limit
        Assert.Single(layers.Units);
        Assert.Equal("/layer/0/layer/0/layer", ex.Location.Pointer);
        Assert.Contains("MaxCompositionDepth", ex.Message, StringComparison.Ordinal);
        Assert.Equal("d.json", ex.Location.File);
    }

    /// <summary>
    ///     Validates that more views than <see cref="VegaLiteLimits.MaxViews"/> is a located render error, and exactly that many are accepted.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_ViewLimitIsEnforced()
    {
        // Arrange: the root and three layers are four views
        const string Json = """{ "layer": [ { "mark": "bar" }, { "mark": "line" }, { "mark": "point" } ] }""";
        var tree = SpecParser.Parse(Json, "v.json");

        // Act
        var accepted = SpecNormalizer.Layers(tree, new VegaLiteLimits { MaxViews = 4 });
        var ex = Assert.Throws<VegaLiteRenderException>(() => SpecNormalizer.Layers(tree, new VegaLiteLimits { MaxViews = 3 }));

        // Assert
        Assert.Equal(3, accepted.Units.Count);
        Assert.Contains("MaxViews", ex.Message, StringComparison.Ordinal);
        Assert.StartsWith("/layer/", ex.Location.Pointer, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the argument is checked.
    /// </summary>
    [Fact]
    public void SpecNormalizer_Layers_NullTreeThrows() =>
        Assert.Throws<ArgumentNullException>(() => SpecNormalizer.Layers(null!));

    /// <summary>
    ///     Validates that an overlay reads like its origin (name, position and pointer) with other properties, and does not take the properties from their parents.
    /// </summary>
    [Fact]
    public void SpecNode_Overlay_KeepsOriginIdentityAndChildrenParents()
    {
        // Arrange
        var tree = SpecParser.Parse("""{ "layer": [ { "mark": "bar", "encoding": { "x": { "field": "a" } } } ], "width": 10 }""");
        var origin = tree.Root["layer"]!.Items[0];
        var encoding = origin["encoding"]!;

        // Act
        var overlay = SpecNode.Overlay(origin, [tree.Root["width"]!, encoding]);

        // Assert: the overlay is the origin for pointers and positions; the values keep their own parents and pointers
        Assert.Equal("/layer/0", overlay.Pointer);
        Assert.Equal((origin.Line, origin.Column, origin.Index), (overlay.Line, overlay.Column, overlay.Index));
        Assert.Same(origin.Parent, overlay.Parent);
        Assert.Same(tree.Root["width"], overlay["width"]);
        Assert.Same(origin, encoding.Parent);
        Assert.Equal("/width", overlay["width"]!.Pointer);
        Assert.Equal("/layer/0/encoding", overlay["encoding"]!.Pointer);
        Assert.Null(overlay["mark"]);
        Assert.Equal(["width", "encoding"], overlay.Properties.Select(p => p.Name));
        Assert.True(overlay.IsObject);
    }

    /// <summary>
    ///     Validates that an overlay with many properties is still searched by name.
    /// </summary>
    [Fact]
    public void SpecNode_Overlay_ManyPropertiesAreFoundByName()
    {
        // Arrange
        var members = string.Join(", ", Enumerable.Range(0, 12).Select(i => $"\"p{i}\": {i}"));
        var tree = SpecParser.Parse("{ " + members + " }");

        // Act
        var overlay = SpecNode.Overlay(tree.Root, tree.Root.Properties.Reverse());

        // Assert
        Assert.Equal(7, overlay["p7"]!.NumberValue);
        Assert.Equal("p11", overlay.Properties[0].Name);
        Assert.Equal(string.Empty, overlay.Pointer);
    }

    /// <summary>
    ///     Validates that the arguments of the overlay factory are checked.
    /// </summary>
    [Fact]
    public void SpecNode_Overlay_NullArgumentsThrow()
    {
        // Arrange
        var tree = SpecParser.Parse("{}");

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => SpecNode.Overlay(null!, []));
        Assert.Throws<ArgumentNullException>(() => SpecNode.Overlay(tree.Root, null!));
    }

    /// <summary>
    ///     Validates that the expressions of a collection can be extended without changing the collection that is extended.
    /// </summary>
    [Fact]
    public void SpecExpressions_With_AddsEntriesToACopy()
    {
        // Arrange
        var tree = SpecParser.Parse("""{ "transform": [ { "calculate": "datum.a", "as": "b" } ], "mark": "bar" }""");
        var existing = tree.Root["transform"]!.Items[0]["calculate"]!;
        var other = tree.Root["mark"]!;
        var extra = ExpressionParser.Parse("1 + 2", other.GetLocation(null));

        // Act
        var extended = tree.Expressions.With([(other, extra)]);
        var replaced = tree.Expressions.With([(existing, extra)]);

        // Assert
        Assert.Equal(1, tree.Expressions.Count);
        Assert.Equal(2, extended.Count);
        Assert.Same(extra, extended.Get(other));
        Assert.Same(tree.Expressions.Get(existing), extended.Get(existing));
        Assert.Null(tree.Expressions.Get(other));
        Assert.Same(extra, replaced.Get(existing));
        Assert.Throws<ArgumentNullException>(() => tree.Expressions.With(null!));
    }

    /// <summary>
    ///     Validates that the temporal fields of several views are collected together.
    /// </summary>
    [Fact]
    public void TemporalFields_CollectAll_IsTheUnionOverTheViews()
    {
        // Arrange
        var (tree, _) = Normalize(
            """
            { "layer": [
                { "mark": "line", "encoding": { "x": { "field": "d", "type": "temporal" } } },
                { "mark": "point", "encoding": { "x": { "field": "e", "type": "temporal" }, "y": { "field": "v", "type": "quantitative" } } } ] }
            """);

        // Act
        var fields = TemporalFields.CollectAll(tree.EnumerateViews());

        // Assert
        Assert.Equal(["d", "e"], fields.OrderBy(f => f, StringComparer.Ordinal));
        Assert.Throws<ArgumentNullException>(() => TemporalFields.CollectAll(null!));
    }
}
