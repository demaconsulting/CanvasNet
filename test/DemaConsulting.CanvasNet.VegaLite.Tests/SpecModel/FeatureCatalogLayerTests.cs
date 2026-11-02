using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.SpecModel;

/// <summary>
///     Unit tests of <see cref="FeatureCatalog"/> for layered views (Phase 10): the layer composition is drawn and not reported, the transforms and the encodings of
///     layered views are visited as the renderer applies them, and the transforms of views that are still not drawn (concat, facet, repeat) say why they are not applied.
/// </summary>
public class FeatureCatalogLayerTests
{
    /// <summary>A transform entry that the renderer computes for a drawn view.</summary>
    private const string Calculate = """{ "calculate": "datum.a * 2", "as": "b" }""";

    /// <summary>
    ///     Parses a specification and lists the findings of the catalog.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <returns>The findings.</returns>
    private static List<FeatureReport> Visit(string json) => [.. FeatureCatalog.Visit(SpecParser.Parse(json))];

    /// <summary>
    ///     Validates the reported bug: a top-level <c>transform</c> of a layered specification is computed by the renderer, so the catalog reports nothing for it, and the
    ///     <c>layer</c> property itself is not reported (the identifier <c>vl-composition-layer</c> is retired).
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_TopLevelTransformOfLayerSpecIsNotReported()
    {
        // Arrange: the specification of the bug report: a top-level calculate over two layers, and a transform of each layer
        const string json = """
            {
              "data": { "values": [ { "a": 1 }, { "a": 2 } ] },
              "transform": [ { "calculate": "datum.a * 2", "as": "b" } ],
              "layer": [
                { "mark": "bar", "transform": [ { "filter": "datum.b > 2" } ], "encoding": { "x": { "field": "a", "type": "ordinal" }, "y": { "field": "b", "type": "quantitative" } } },
                { "mark": "rule", "transform": [ { "aggregate": [ { "op": "mean", "field": "b", "as": "m" } ] } ], "encoding": { "y": { "field": "m", "type": "quantitative" } } }
              ]
            }
            """;

        // Act
        var reports = Visit(json);

        // Assert
        Assert.Empty(reports);
    }

    /// <summary>
    ///     Validates that the transforms of a view that is not drawn say which composition keeps them from being applied, for each composition, with the identifier
    ///     <c>vl-transform-&lt;name&gt;</c> unchanged.
    /// </summary>
    /// <param name="composition">The composition the view is in.</param>
    /// <param name="json">The specification.</param>
    /// <param name="pointer">The pointer of the transform.</param>
    [Theory]
    [InlineData("concat", """{ "concat": [ { "transform": [ { "calculate": "datum.a * 2", "as": "b" } ], "mark": "point" } ] }""", "/concat/0/transform/0")]
    [InlineData("hconcat", """{ "hconcat": [ { "transform": [ { "calculate": "datum.a * 2", "as": "b" } ], "mark": "point" } ] }""", "/hconcat/0/transform/0")]
    [InlineData("vconcat", """{ "vconcat": [ { "transform": [ { "calculate": "datum.a * 2", "as": "b" } ], "mark": "point" } ] }""", "/vconcat/0/transform/0")]
    [InlineData("facet", """{ "facet": { "field": "f", "type": "nominal" }, "spec": { "transform": [ { "calculate": "datum.a * 2", "as": "b" } ], "mark": "point" } }""", "/spec/transform/0")]
    [InlineData("repeat", """{ "repeat": [ "a" ], "spec": { "transform": [ { "calculate": "datum.a * 2", "as": "b" } ], "mark": "point" } }""", "/spec/transform/0")]
    public void FeatureCatalog_Visit_TransformOfConcatViewMessageNamesComposition(string composition, string json, string pointer)
    {
        // Act
        var report = Assert.Single(Visit(json), r => r.Id == "vl-transform-calculate");

        // Assert: the identifier is the old one, the message names the composition and no longer claims that calculate is unsupported
        Assert.Equal(FeatureReportKind.UnsupportedFeature, report.Kind);
        Assert.Equal(pointer, report.Location.Pointer);
        Assert.Equal($"The 'calculate' transform is not applied: the view is a '{composition}' composition, which is not supported yet.", report.Message);
    }

    /// <summary>
    ///     Validates that a layered view inside a concatenation is not drawn, so its transforms and those of its layers name the concatenation, and the layers of a drawn view
    ///     are not reported.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_TransformOfLayerInsideConcatNamesTheConcatenation()
    {
        // Arrange: a layered view and its layer inside an hconcat
        const string json = """
            {
              "hconcat": [
                { "transform": [ { "calculate": "datum.a * 2", "as": "b" } ],
                  "layer": [ { "mark": "bar", "transform": [ { "filter": "datum.b > 1" } ] } ] }
              ]
            }
            """;

        // Act
        var reports = Visit(json);

        // Assert
        Assert.Equal(
            [
                ("vl-composition-hconcat", "/hconcat"),
                ("vl-transform-calculate", "/hconcat/0/transform/0"),
                ("vl-transform-filter", "/hconcat/0/layer/0/transform/0"),
            ],
            reports.Select(r => (r.Id, r.Location.Pointer)));
        Assert.All(reports.Skip(1), r => Assert.Contains("is a 'hconcat' composition", r.Message, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that a transform of a later phase in a drawn layer keeps its original message.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_TransformOfLaterPhaseInLayerIsNotSupportedYet()
    {
        // Arrange
        const string json = """{ "layer": [ { "mark": "line", "transform": [ { "loess": "y", "on": "x" } ] } ] }""";

        // Act
        var report = Assert.Single(Visit(json));

        // Assert
        Assert.Equal("vl-transform-loess", report.Id);
        Assert.Equal("The 'loess' transform is not supported yet.", report.Message);
        Assert.Equal("/layer/0/transform/0", report.Location.Pointer);
    }

    /// <summary>
    ///     Validates that the transforms of a layered view and of its layers are all visited as drawn: their unsupported parts are reported, nothing else is.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_LayerTransformsReportOnlyTheirUnsupportedParts()
    {
        // Arrange: a calculate with a function outside the whitelist at the top level and at a layer, and a filter on a selection
        var json = $$"""
            {
              "transform": [ { "calculate": "format(datum.a, '.1f')", "as": "b" }, {{Calculate}} ],
              "layer": [ { "mark": "point", "transform": [ { "calculate": "format(datum.a, '.1f')", "as": "c" } ] } ]
            }
            """;

        // Act
        var reports = Visit(json);

        // Assert
        Assert.Equal(
            [("vl-expr-function-format", "/transform/0/calculate"), ("vl-expr-function-format", "/layer/0/transform/0/calculate")],
            reports.Select(r => (r.Id, r.Location.Pointer)));
    }

    /// <summary>
    ///     Validates that a view inside a layered view that sets what only the top-level view decides gets one warning for each property.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_LayerPropertiesOfTheTopLevelViewAreWarnedAboutInALayer()
    {
        // Arrange: the layer sets the title, the size, the background, the padding, the autosize, a config and datasets; the top-level view may set them
        const string json = """
            {
              "title": "ok", "width": 100, "background": "white", "padding": 5, "config": { "view": { "stroke": null } }, "datasets": { "d": [ { "a": 1 } ] },
              "layer": [
                { "mark": "point", "title": "t", "width": 50, "height": 50, "background": "red", "padding": 1, "autosize": "pad", "config": {}, "datasets": {} }
              ]
            }
            """;

        // Act
        var reports = Visit(json);

        // Assert
        Assert.All(reports, r => Assert.Equal(("vl-layer-property-ignored", FeatureReportKind.Warning), (r.Id, r.Kind)));
        Assert.Equal(
            ["/layer/0/title", "/layer/0/width", "/layer/0/height", "/layer/0/background", "/layer/0/padding", "/layer/0/autosize", "/layer/0/config", "/layer/0/datasets"],
            reports.Select(r => r.Location.Pointer));
        Assert.Contains("'title' of a view inside a layer is ignored", reports[0].Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the encoding of a layered view is visited with the mark of each layer it is merged into, and that a finding the layers share is reported once.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_InheritedEncodingIsVisitedPerLayerAndReportedOnce()
    {
        // Arrange: a shape channel at the top level that no bar draws (two layers, the same message), and an x2 on the top level that the point layer cannot draw
        const string json = """
            {
              "encoding": { "shape": { "field": "s", "type": "nominal" }, "x": { "field": "a", "type": "quantitative" }, "x2": { "field": "b" } },
              "layer": [
                { "mark": "bar" },
                { "mark": "bar", "encoding": { "color": { "field": "c", "type": "nominal" } } },
                { "mark": "point" }
              ]
            }
            """;

        // Act
        var reports = Visit(json);

        // Assert: the bars share one report for the shape, the point draws a shape but cannot draw x2; the findings are at the top-level channels
        Assert.Equal(
            [("vl-channel-shape", "/encoding/shape"), ("vl-channel-x2", "/encoding/x2")],
            reports.Select(r => (r.Id, r.Location.Pointer)));
        Assert.Contains("'bar' mark", reports[0].Message, StringComparison.Ordinal);
        Assert.Contains("'x2' channel is only supported for a bar, rect, rule, area, errorbar or errorband", reports[1].Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the layered view itself and its nested layered views are not reported as compositions and that the root mark of a layered view is ignored.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_LayerCompositionIsNeverReported()
    {
        // Arrange: nested layers of drawn marks
        const string json = """{ "layer": [ { "layer": [ { "mark": "bar" }, { "mark": "line" } ] }, { "mark": "point" } ] }""";

        // Act / Assert
        Assert.Empty(Visit(json));
        Assert.DoesNotContain(Visit("""{ "layer": [ { "mark": "geoshape" } ] }"""), r => r.Id == "vl-composition-layer");
    }
}
