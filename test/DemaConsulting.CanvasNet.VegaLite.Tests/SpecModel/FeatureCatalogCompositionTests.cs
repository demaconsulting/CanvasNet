using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.SpecModel;

/// <summary>
///     Unit tests of <see cref="FeatureCatalog"/> for compositions (Phase 11): a concatenation, a repeat and a facet are drawn and not reported, the properties that
///     a composition ignores are warned about, the parts of a facet that are not drawn (the transforms of its field, a sort by an aggregate, the sides of the header) are reported,
///     and the views of a composition are visited as the renderer draws them.
/// </summary>
public class FeatureCatalogCompositionTests
{
    /// <summary>
    ///     Parses a specification and lists the findings of the catalog.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <returns>The findings.</returns>
    private static List<FeatureReport> Visit(string json) => [.. FeatureCatalog.Visit(SpecParser.Parse(json))];

    /// <summary>
    ///     Lists the identifier and the pointer of each finding.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <returns>The identifiers and pointers in document order.</returns>
    private static List<(string Id, string Pointer)> Ids(string json) => [.. Visit(json).Select(r => (r.Id, r.Location.Pointer))];

    /// <summary>
    ///     Validates that a concatenation and a repeat are drawn, so no identifier names them and nothing is reported for the views inside.
    /// </summary>
    /// <param name="json">The specification.</param>
    [Theory]
    [InlineData("""{ "concat": [ { "mark": "point" }, { "mark": "bar" } ] }""")]
    [InlineData("""{ "hconcat": [ { "mark": "point" }, { "mark": "bar" } ] }""")]
    [InlineData("""{ "vconcat": [ { "mark": "point" }, { "hconcat": [ { "mark": "bar" } ] } ] }""")]
    [InlineData("""{ "repeat": [ "a", "b" ], "spec": { "mark": "point", "encoding": { "x": { "field": { "repeat": "repeat" }, "type": "quantitative" } } } }""")]
    [InlineData("""{ "repeat": { "row": [ "a" ], "column": [ "b" ] }, "spec": { "mark": "point", "encoding": { "x": { "field": { "repeat": "column" }, "type": "quantitative" }, "y": { "field": { "repeat": "row" }, "type": "quantitative" } } } }""")]
    [InlineData("""{ "repeat": { "layer": [ "a", "b" ] }, "spec": { "mark": "line", "encoding": { "y": { "field": { "repeat": "layer" }, "type": "quantitative" } } } }""")]
    public void FeatureCatalog_Visit_ConcatAndRepeatAreNotReported(string json)
    {
        // Act
        var reports = Visit(json);

        // Assert
        Assert.Empty(reports);
    }

    /// <summary>
    ///     Validates that a facet, and a unit view with a faceting channel (which is a facet), are drawn and not reported: the identifier <c>vl-composition-facet</c> is retired,
    ///     their transforms are applied, and their field definitions (with a header) report nothing when they use only what is drawn.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_FacetAndFacetedUnitViewAreNotReported()
    {
        // Arrange
        const string facet = """{ "facet": { "field": "f", "type": "nominal" }, "spec": { "mark": "point" } }""";
        const string mapping = """{ "facet": { "row": { "field": "r", "type": "ordinal", "sort": "descending", "header": { "labelAngle": 0, "title": null } }, "column": { "field": "c", "type": "nominal", "sort": [ "b", "a" ] } }, "spec": { "mark": "point" } }""";
        const string faceted = """{ "mark": "point", "transform": [ { "calculate": "1", "as": "x" } ], "encoding": { "row": { "field": "f", "type": "nominal" }, "column": { "field": "g", "type": "nominal" } } }""";
        const string wrapped = """{ "mark": "point", "encoding": { "facet": { "field": "f", "type": "nominal", "columns": 2 } } }""";

        // Act / Assert
        Assert.Empty(Visit(facet));
        Assert.Empty(Visit(mapping));
        Assert.Empty(Visit(faceted));
        Assert.Empty(Visit(wrapped));
    }

    /// <summary>
    ///     Validates that the faceting channels of a unit view that cannot be faceted (a layer, or the <c>spec</c> of a facet) are still reported with their own identifier
    ///     and a message that says why, and that the other channels of the view are not affected.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_RowColumnChannelInLayerOrFacetSpecStillReported()
    {
        // Arrange
        const string layer = """{ "layer": [ { "mark": "point", "encoding": { "row": { "field": "r", "type": "nominal" }, "x": { "field": "a", "type": "quantitative" } } } ] }""";
        const string inherited = """{ "encoding": { "column": { "field": "c", "type": "nominal" } }, "layer": [ { "mark": "point" }, { "mark": "bar" } ] }""";
        const string inSpec = """{ "facet": { "field": "f", "type": "nominal" }, "spec": { "mark": "point", "encoding": { "facet": { "field": "g", "type": "nominal" } } } }""";

        // Act
        var layerReports = Visit(layer);
        var inheritedReports = Ids(inherited);
        var specReports = Visit(inSpec);

        // Assert
        var row = Assert.Single(layerReports);
        Assert.Equal(("vl-channel-row", "/layer/0/encoding/row"), (row.Id, row.Location.Pointer));
        Assert.Equal(FeatureReportKind.UnsupportedFeature, row.Kind);
        Assert.Contains("only facets a unit view that is not a layer or the spec of a facet", row.Message, StringComparison.Ordinal);
        Assert.Equal([("vl-channel-column", "/encoding/column")], inheritedReports);
        var specReport = Assert.Single(specReports);
        Assert.Equal(("vl-channel-facet", "/spec/encoding/facet"), (specReport.Id, specReport.Location.Pointer));
    }

    /// <summary>
    ///     Validates the reports about the field definition of a facet: a <c>bin</c>, <c>timeUnit</c> or <c>aggregate</c> that is not applied, in the facet operator and in the
    ///     channel of a unit view, each at its property, with the one identifier <c>vl-facet-field-transform</c>.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_FacetFieldTransformIsReported()
    {
        // Arrange
        const string facet = """
            { "facet": { "row": { "field": "r", "type": "quantitative", "bin": true }, "column": { "field": "c", "type": "temporal", "timeUnit": "month" } }, "spec": { "mark": "point" } }
            """;
        const string channel = """{ "mark": "point", "encoding": { "column": { "field": "c", "type": "quantitative", "aggregate": "mean" } } }""";
        const string off = """{ "facet": { "field": "f", "type": "nominal", "bin": false, "timeUnit": null, "aggregate": null }, "spec": { "mark": "point" } }""";

        // Act / Assert
        Assert.Equal([("vl-facet-field-transform", "/facet/row/bin"), ("vl-facet-field-transform", "/facet/column/timeUnit")], Ids(facet));
        Assert.Equal([("vl-facet-field-transform", "/encoding/column/aggregate")], Ids(channel));
        Assert.Empty(Visit(off));
    }

    /// <summary>
    ///     Validates that a facet sort by a field and an operation is reported at the sort, while ascending, descending, null and an array of values are not.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_FacetSortByAggregateIsReported()
    {
        // Arrange
        const string byField = """{ "facet": { "field": "f", "type": "nominal", "sort": { "field": "v", "op": "mean" } }, "spec": { "mark": "point" } }""";
        const string byOp = """{ "mark": "point", "encoding": { "row": { "field": "f", "type": "nominal", "sort": { "op": "count", "order": "descending" } } } }""";
        const string plain = """{ "facet": { "field": "f", "type": "nominal", "sort": null }, "spec": { "mark": "point" } }""";

        // Act / Assert
        Assert.Equal([("vl-facet-sort", "/facet/sort")], Ids(byField));
        Assert.Equal([("vl-facet-sort", "/encoding/row/sort")], Ids(byOp));
        Assert.Empty(Visit(plain));
    }

    /// <summary>
    ///     Validates the header reports: a <c>labelExpr</c> is evaluated and reports only what its expression cannot do (<c>vl-expr-function-format</c>), a custom <c>formatType</c> reports <c>vl-format-type-custom</c>, and a side that the header does not
    ///     draw on reports <c>vl-header-orient</c> (a side along the channel, such as the bottom of a column header, is drawn and not reported).
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_HeaderOrientExprAndFormatTypeAreReported()
    {
        // Arrange
        const string json = """
            {
              "facet": {
                "row": { "field": "r", "type": "nominal", "header": { "labelExpr": "format(datum.value, '.0f')", "labelOrient": "top", "titleOrient": "right" } },
                "column": { "field": "c", "type": "nominal", "header": { "formatType": "custom", "labelOrient": "bottom", "titleOrient": "left" } }
              },
              "spec": { "mark": "point" }
            }
            """;
        const string wrapped = """{ "facet": { "field": "f", "type": "nominal", "header": { "labelOrient": "left", "titleOrient": "right" } }, "spec": { "mark": "point" } }""";

        // Act
        var ids = Ids(json);
        var wrappedIds = Ids(wrapped);

        // Assert: a row title may be on the right, a row label not on top; a column label may be at the bottom, a column title not on the left; a wrapped facet's labels are cell titles on any side
        Assert.Equal(
            [
                ("vl-expr-function-format", "/facet/row/header/labelExpr"),
                ("vl-header-orient", "/facet/row/header/labelOrient"),
                ("vl-format-type-custom", "/facet/column/header/formatType"),
                ("vl-header-orient", "/facet/column/header/titleOrient"),
            ],
            ids);
        Assert.Equal([("vl-header-orient", "/facet/header/titleOrient")], wrappedIds);
    }

    /// <summary>
    ///     Validates the warning for the properties that a composition ignores: the <c>width</c>, <c>height</c> and an <c>autosize</c> other than <c>pad</c> of a concatenation, a
    ///     repeat of rows or columns, and a facet, each at its property and with no unsupported-feature report for the autosize type; <c>pad</c> and a repeat of layers (which is
    ///     a layered view and has a size) are not warned about.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_CompositionPropertiesIgnoredAreWarnedAbout()
    {
        // Arrange
        const string concat = """{ "width": 100, "height": 50, "autosize": "fit", "hconcat": [ { "mark": "point" } ] }""";
        const string repeat = """{ "width": 100, "autosize": { "type": "fit-x", "contains": "padding" }, "repeat": { "row": [ "a" ] }, "spec": { "mark": "point" } }""";
        const string padded = """{ "autosize": "pad", "vconcat": [ { "mark": "point" } ] }""";
        const string layerRepeat = """{ "width": 100, "repeat": { "layer": [ "a" ] }, "spec": { "mark": "point" } }""";

        // Act
        var concatReports = Visit(concat);

        // Assert
        Assert.Equal(
            [("vl-composition-property-ignored", "/width"), ("vl-composition-property-ignored", "/height"), ("vl-composition-property-ignored", "/autosize")],
            concatReports.Select(r => (r.Id, r.Location.Pointer)));
        Assert.All(concatReports, r => Assert.Equal(FeatureReportKind.Warning, r.Kind));
        Assert.Contains("autosize type 'fit'", concatReports[2].Message, StringComparison.Ordinal);
        Assert.Equal(
            [("vl-composition-property-ignored", "/width"), ("vl-composition-property-ignored", "/autosize/type")],
            Ids(repeat));
        Assert.Empty(Visit(padded));
        Assert.Empty(Visit(layerRepeat));
    }

    /// <summary>
    ///     Validates the warning for the properties of a view inside a composition that only the top-level view decides (<c>padding</c>, <c>background</c>, <c>config</c>,
    ///     <c>datasets</c>, <c>autosize</c>) and for a <c>"container"</c> size, in a child of a concatenation, in the <c>spec</c> of a repeat, and in a facet, but not for the
    ///     size of the view.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_NestedViewPropertiesIgnoredAreWarnedAbout()
    {
        // Arrange
        const string concat = """
            {
              "hconcat": [
                { "width": "container", "height": 50, "padding": 5, "background": "red", "config": {}, "datasets": {}, "autosize": "pad", "mark": "point" }
              ]
            }
            """;
        const string repeat = """{ "repeat": [ "a" ], "spec": { "padding": 5, "mark": "point" } }""";

        // Act
        var concatIds = Ids(concat);
        var repeatIds = Ids(repeat);

        // Assert
        Assert.Equal(
            [
                ("vl-composition-property-ignored", "/hconcat/0/width"),
                ("vl-composition-property-ignored", "/hconcat/0/padding"),
                ("vl-composition-property-ignored", "/hconcat/0/background"),
                ("vl-composition-property-ignored", "/hconcat/0/config"),
                ("vl-composition-property-ignored", "/hconcat/0/datasets"),
                ("vl-composition-property-ignored", "/hconcat/0/autosize"),
            ],
            concatIds);
        Assert.Equal([("vl-composition-property-ignored", "/spec/padding")], repeatIds);
    }

    /// <summary>
    ///     Validates that the encoding of a unit view in a concatenation is visited exactly once (it is the view's own, nothing is merged into it), and that the encoding that
    ///     a layered view in a concatenation passes to its layers is reported once at the place where it is written.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_ConcatChildEncodingIsVisitedOnce()
    {
        // Arrange
        const string unit = """{ "hconcat": [ { "mark": "point", "encoding": { "longitude": { "field": "a", "type": "quantitative" } } } ] }""";
        const string layered = """
            {
              "vconcat": [
                { "encoding": { "latitude": { "field": "a", "type": "quantitative" } }, "layer": [ { "mark": "point" }, { "mark": "bar" } ] }
              ]
            }
            """;

        // Act
        var unitIds = Ids(unit);
        var layeredIds = Ids(layered);

        // Assert
        Assert.Equal([("vl-channel-geo", "/hconcat/0/encoding/longitude")], unitIds);
        Assert.Equal([("vl-channel-geo", "/vconcat/0/encoding/latitude")], layeredIds);
    }

    /// <summary>
    ///     Validates that the encoding of a repeated view is visited for the copies the renderer draws (with the repeated field in place) and reported once at the
    ///     template, however many copies there are.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_RepeatedUnitEncodingIsVisitedPerCopy()
    {
        // Arrange
        const string json = """
            {
              "repeat": { "row": [ "a", "b" ], "column": [ "c", "d" ] },
              "spec": {
                "mark": "point",
                "encoding": { "x": { "field": { "repeat": "column" }, "type": "quantitative", "axis": { "labelBound": true } },
                              "y": { "field": { "repeat": "row" }, "type": "quantitative" } }
              }
            }
            """;

        // Act
        var ids = Ids(json);

        // Assert: four copies, one report
        Assert.Equal([("vl-guide-axis-labelbound", "/spec/encoding/x/axis/labelBound")], ids);
    }

    /// <summary>
    ///     Validates that the warnings the renderer produces from the view tree are not produced by the catalog, so each is delivered once: a shared axis of a concatenation, a
    ///     <c>columns</c> that a repeat of rows ignores, and a repeat reference that is not defined.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_TreeWarningsAreLeftToTheRenderer()
    {
        // Arrange
        const string json = """
            {
              "resolve": { "axis": { "x": "shared" } },
              "repeat": { "row": [ "a" ] }, "columns": 2,
              "spec": { "mark": "point", "encoding": { "x": { "field": { "repeat": "column" }, "type": "quantitative" } } }
            }
            """;

        // Act
        var ids = Ids(json);

        // Assert
        Assert.DoesNotContain(ids, i => i.Id is "vl-concat-axis-shared" or "vl-composition-columns-ignored" or "vl-repeat-ref-unresolved");
    }
}
