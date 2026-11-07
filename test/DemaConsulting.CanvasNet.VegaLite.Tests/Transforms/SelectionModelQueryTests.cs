using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;

/// <summary>
///     Unit tests of what the later stages ask of <see cref="SelectionModel"/> (Phase 12, Vega-Lite's <c>getSelectionComponent</c>, <c>parseSelectionExtent</c> and the scale bindings): the units
///     a model is bound to, the selection a view sees, the selection bound to a scale, the extent that a scale or a bin reads from a selection, the brush mark and the state of a selection bound
///     to the scales of a view that is not the whole view.
/// </summary>
public class SelectionModelQueryTests
{
    /// <summary>The unit view of the charts: a point mark with x on <c>a</c> and y on <c>b</c>.</summary>
    private const string Unit = """ "mark": "point", "encoding": { "x": { "field": "a", "type": "quantitative" }, "y": { "field": "b", "type": "quantitative" } } """;

    /// <summary>
    ///     Builds the model of a specification, bound to its units, as the document does.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="config">The caller's configuration text, or null.</param>
    /// <returns>The model and the layer tree it is bound to.</returns>
    private static (SelectionModel Model, LayerTree Layers) Bound(string json, string? config = null)
    {
        var tree = SpecParser.Parse(json);
        var resolved = ConfigResolver.Resolve(tree, config);
        var layers = SpecNormalizer.Expand(tree, resolved).Layers;
        var model = SelectionModel.Create(tree, layers, resolved, null, TimeCalendar.Utc, new ExpressionFunctionContext(null, CancellationToken.None));
        return (model.BindUnits(layers), layers);
    }

    /// <summary>
    ///     Builds a chart of two views side by side with a top-level selection parameter.
    /// </summary>
    /// <param name="parameter">The parameter object text.</param>
    /// <returns>The specification text.</returns>
    private static string Concat(string parameter) =>
        "{ \"params\": [ " + parameter + " ], \"data\": { \"values\": [ { \"a\": 1, \"b\": 1 } ] }, \"hconcat\": [ { " + Unit + " }, { \"name\": \"second\", " + Unit + " } ] }";

    /// <summary>
    ///     Validates that a model is bound to the units of the layer tree by position: each unit has the components declared for it, a top-level selection of a concatenation is declared for every
    ///     unit, and a selection that names a view with <c>views</c> is declared for that view only.
    /// </summary>
    [Fact]
    public void SelectionModel_ComponentsOf_AreTheSelectionsDeclaredForTheUnit()
    {
        // Arrange
        var (all, allLayers) = Bound(Concat("""{ "name": "brush", "select": "interval" }"""));
        var (named, namedLayers) = Bound(Concat("""{ "name": "brush", "select": "interval", "views": ["second"] }"""));

        // Assert
        Assert.Equal([1, 1], allLayers.Units.Select(u => all.ComponentsOf(u).Count()));
        Assert.Equal([0, 1], namedLayers.Units.Select(u => named.ComponentsOf(u).Count()));
    }

    /// <summary>
    ///     Validates that a model that is not bound to a layer tree knows no unit.
    /// </summary>
    [Fact]
    public void SelectionModel_ComponentsOf_ANodeOfAnotherTreeHasNone()
    {
        // Arrange: the model of one parse, asked about the units of another
        var (model, _) = Bound(Concat("""{ "name": "brush", "select": "interval" }"""));
        var other = SpecNormalizer.Expand(SpecParser.Parse(Concat("""{ "name": "brush", "select": "interval" }""")), ConfigResolver.Defaults).Layers;

        // Act / Assert
        Assert.All(other.Units, u => Assert.Empty(model.ComponentsOf(u)));
    }

    /// <summary>
    ///     Validates the selection that a view sees: a unit sees its own, a unit that has none sees that of the view above it (the last unit of that view that declares the name), and a name that no
    ///     selection has is not found.
    /// </summary>
    [Fact]
    public void SelectionModel_FindComponent_WalksUpToTheViewsAbove()
    {
        // Arrange: the selection is declared for the second view only
        var (model, layers) = Bound(Concat("""{ "name": "brush", "select": "interval", "views": ["second"] }"""));

        // Act
        var seenByFirst = model.FindComponent(layers.Units[0], "brush");
        var seenByRoot = model.FindComponent(layers.Root, "brush");
        var own = model.FindComponent(layers.Units[1], "brush");

        // Assert: the first view sees it through the concatenation
        Assert.NotNull(seenByFirst);
        Assert.Same(own, seenByFirst);
        Assert.Same(own, seenByRoot);
        Assert.Equal("second", own!.UnitName);
        Assert.Null(model.FindComponent(layers.Units[0], "other"));
    }

    /// <summary>
    ///     Validates that a selection bound to the scales of a unit gives the field it constrains on the channel, only for an interval selection that is resolved globally and projects the channel over a
    ///     continuous domain.
    /// </summary>
    [Fact]
    public void SelectionModel_BoundExtentOf_IsAnIntervalBoundToAContinuousScale()
    {
        // Arrange
        string View(string select, string bind) => "{ \"params\": [ { \"name\": \"zoom\", \"select\": " + select + ", " + bind + " \"value\": { \"x\": [2, 6] } } ], \"data\": { \"values\": [ { \"a\": 1, \"b\": 1 } ] }, " + Unit + " }";
        var (bound, boundLayers) = Bound(View("""{ "type": "interval", "encodings": ["x"] }""", """ "bind": "scales", """));
        var (unbound, unboundLayers) = Bound(View("""{ "type": "interval", "encodings": ["x"] }""", string.Empty));
        var (union, unionLayers) = Bound(View("""{ "type": "interval", "encodings": ["x"], "resolve": "union" }""", """ "bind": "scales", """));

        // Act
        var found = bound.BoundExtentOf(boundLayers.Units[0], "x");

        // Assert: x is bound through the field a; y is not projected; an unbound or a union selection binds nothing
        Assert.Equal("a", found!.Value.Field);
        Assert.Equal("zoom", found.Value.Component.Name);
        Assert.Null(bound.BoundExtentOf(boundLayers.Units[0], "y"));
        Assert.Null(unbound.BoundExtentOf(unboundLayers.Units[0], "x"));
        Assert.Null(union.BoundExtentOf(unionLayers.Units[0], "x"));
        Assert.True(bound.Components[0].IsScaleBound);
        Assert.False(union.Components[0].IsScaleBound);
    }

    /// <summary>
    ///     Validates the extent that a selection gives (Vega-Lite's <c>parseSelectionExtent</c>): the range of the field the extent names, of the field a channel is projected over, or of the first
    ///     projected field, reporting the choice of the first field when several are projected and the encoding is not exactly one.
    /// </summary>
    [Fact]
    public void SelectionModel_ResolveExtent_ReadsTheSelectedRangeOfAField()
    {
        // Arrange: x over a is [2, 6] and y over b is [1, 3]
        var (model, layers) = Bound("""
            { "params": [ { "name": "brush", "select": "interval", "value": { "x": [2, 6], "y": [1, 3] } } ],
              "data": { "values": [ { "a": 1, "b": 1 } ] }, "mark": "point",
              "encoding": { "x": { "field": "a", "type": "quantitative" }, "y": { "field": "b", "type": "quantitative" } } }
            """);
        var unit = layers.Units[0];
        var warnings = new List<FeatureReport>();
        var at = new VegaLiteLocation("test", "/x", 1, 1);

        // Act
        var byField = model.ResolveExtent(unit, "brush", "b", null, at, warnings);
        var byEncoding = model.ResolveExtent(unit, "brush", null, "y", at, warnings);
        var noWarning = warnings.Count;
        var first = model.ResolveExtent(unit, "brush", null, null, at, warnings);
        var wrongEncoding = model.ResolveExtent(unit, "brush", null, "color", at, warnings);

        // Assert
        Assert.True(byField.IsSelection);
        Assert.Equal([1.0, 3.0], byField.Value.Elements.ToArray().Select(v => v.Number));
        Assert.Equal([1.0, 3.0], byEncoding.Value.Elements.ToArray().Select(v => v.Number));
        Assert.Equal(0, noWarning);
        Assert.Equal([2.0, 6.0], first.Value.Elements.ToArray().Select(v => v.Number));
        Assert.Equal([2.0, 6.0], wrongEncoding.Value.Elements.ToArray().Select(v => v.Number));

        // Both choices of the first field are reported, once each for the location
        Assert.Single(warnings);
        Assert.Equal("vl-param-selection-projection", warnings[0].Id);
    }

    /// <summary>
    ///     Validates that a name that is not a selection is not read (the caller reads the variable parameter itself), and that a selection that holds nothing for the field has no value.
    /// </summary>
    [Fact]
    public void SelectionModel_ResolveExtent_AVariableParameterIsNotASelection()
    {
        // Arrange: an interval over x without a value
        var (model, layers) = Bound("""
            { "params": [ { "name": "brush", "select": { "type": "interval", "encodings": ["x"] } }, { "name": "plain", "value": [2, 3] } ],
              "data": { "values": [ { "a": 1, "b": 1 } ] }, "mark": "point", "encoding": { "x": { "field": "a", "type": "quantitative" } } }
            """);
        var at = new VegaLiteLocation("test", "/y", 1, 1);

        // Act
        var variable = model.ResolveExtent(layers.Units[0], "plain", null, null, at, null);
        var empty = model.ResolveExtent(layers.Units[0], "brush", null, null, at, null);

        // Assert
        Assert.False(variable.IsSelection);
        Assert.True(empty.IsSelection);
        Assert.True(empty.Value.IsNull);
    }

    /// <summary>
    ///     Validates the state of a selection bound to the scales: in a view that is the whole view it is what its value says, in a unit of a concatenation the top-level signal is rebuilt from the signals
    ///     of the scales, which hold nothing in a static chart (the selection reads as an empty object), though the store keeps its tuple.
    /// </summary>
    [Fact]
    public void SelectionModel_Create_ABindingInAConcatenationHoldsNothing()
    {
        // Arrange
        const string parameter = """{ "name": "zoom", "select": { "type": "interval", "encodings": ["x"] }, "bind": "scales", "value": { "x": [2, 6] } }""";
        var (single, _) = Bound("{ \"params\": [ " + parameter + " ], \"data\": { \"values\": [ { \"a\": 1, \"b\": 1 } ] }, " + Unit + " }");
        var (concat, _) = Bound(Concat(parameter));

        // Act
        var whole = single.Values["zoom"].ObjectValue!;
        var nested = concat.Values["zoom"].ObjectValue!;

        // Assert
        Assert.True(whole.TryGetValue("a", out var range));
        Assert.Equal([2.0, 6.0], range.Elements.ToArray().Select(v => v.Number));
        Assert.False(nested.TryGetValue("a", out _));
        Assert.Single(concat.Stores["zoom_store"].Entries);
        Assert.True(single.Components[0].IsTopLevelView);
        Assert.False(concat.Components[0].IsTopLevelView);
    }

    /// <summary>
    ///     Validates the brush mark of a component: the default mark of the interval type, merged under the <c>mark</c> of the selection and the configuration, property by property; a point selection has none.
    /// </summary>
    [Fact]
    public void SelectionModel_Create_TheMarkIsTheConfigurationMergedUnderTheSelection()
    {
        // Arrange
        string View(string select) => "{ \"params\": [ { \"name\": \"s\", \"select\": " + select + " } ], \"data\": { \"values\": [ { \"a\": 1, \"b\": 1 } ] }, " + Unit + " }";

        // Act
        var plain = Bound(View("\"interval\"")).Model.Components[0].Mark!;
        var own = Bound(View("""{ "type": "interval", "mark": { "stroke": "red", "strokeWidth": 3 } }""")).Model.Components[0].Mark!;
        var configured = Bound(View("\"interval\""), """{ "selection": { "interval": { "mark": { "fill": "green" } } } }""").Model.Components[0].Mark!;
        var point = Bound(View("\"point\"")).Model.Components[0].Mark;

        // Assert
        Assert.Equal(("#333", 0.125, "white"), (plain["fill"]!.StringValue, plain["fillOpacity"]!.NumberValue, plain["stroke"]!.StringValue));
        Assert.Equal(("#333", "red", 3.0), (own["fill"]!.StringValue, own["stroke"]!.StringValue, own["strokeWidth"]!.NumberValue));
        Assert.Equal(("green", 0.125, "white"), (configured["fill"]!.StringValue, configured["fillOpacity"]!.NumberValue, configured["stroke"]!.StringValue));
        Assert.Null(point);
    }

    /// <summary>
    ///     Validates the configuration of the selection types: the defaults of Vega-Lite (a global resolution, the projection over x and y for an interval) are in the effective configuration, and the
    ///     configuration of the caller changes the resolution of every selection that does not say.
    /// </summary>
    [Fact]
    public void SelectionModel_Create_TheResolutionComesFromTheConfiguration()
    {
        // Arrange
        const string json = """{ "params": [ { "name": "s", "select": "interval" } ], "data": { "values": [ { "a": 1 } ] }, "mark": "point", "encoding": { "x": { "field": "a", "type": "quantitative" } } }""";

        // Act
        var defaults = Bound(json).Model.Components[0];
        var configured = Bound(json, """{ "selection": { "interval": { "resolve": "union", "encodings": ["x"] } } }""").Model.Components[0];

        // Assert
        Assert.Equal(("global", true), (defaults.Resolve, defaults.IsTopLevelView));
        Assert.Equal("union", configured.Resolve);
        Assert.Equal(["a/x"], configured.Projection.Select(p => p.Field + "/" + p.Channel));
    }
}
