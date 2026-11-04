using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.SpecModel;

/// <summary>
///     Tests of what the parser and the feature catalog do with <c>labelExpr</c> (compiled when the document is opened, reported only for what its expression cannot do; <c>vl-expr-label</c> is retired) and with the offset
///     channels (<c>vl-offset-position</c>, <c>vl-offset-ranged</c>, and the <c>for</c> of a step size).
/// </summary>
public class FeatureCatalogLabelExprAndOffsetTests
{
    /// <summary>
    ///     Reports the unsupported constructs of a specification.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <returns>The identifier and the pointer of each report in order.</returns>
    private static List<(string Id, string Pointer)> Visit(string json) =>
        FeatureCatalog.Visit(SpecParser.Parse(json)).Select(r => (r.Id, r.Location.Pointer)).ToList();

    /// <summary>
    ///     Builds a bar chart whose x channel has the given definition properties.
    /// </summary>
    /// <param name="x">The JSON of the x channel.</param>
    /// <param name="top">More top-level properties starting with a comma.</param>
    /// <returns>The specification text.</returns>
    private static string Chart(string x, string top = "") =>
        "{ \"mark\": \"bar\", \"encoding\": { \"x\": " + x + ", \"y\": { \"field\": \"v\", \"type\": \"quantitative\" } }" + top + " }";

    /// <summary>
    ///     Validates that a label expression that only uses supported functions is not reported anywhere: axis, legend, header and the configuration.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_SupportedLabelExpressionsAreNotReported()
    {
        // Arrange
        const string json = """
            { "config": { "axis": { "labelExpr": "datum.label + '%'" }, "axisX": { "labelExpr": "upper(datum.label)" }, "header": { "labelExpr": "datum.value" }, "headerRow": { "labelExpr": "datum.label" } },
              "mark": "point",
              "encoding": {
                "x": { "field": "a", "type": "quantitative", "axis": { "labelExpr": "datum.value % 2 ? '' : datum.label" } },
                "color": { "field": "b", "type": "nominal", "legend": { "labelExpr": "datum.index + datum.label" } } } }
            """;
        const string facet = """{ "facet": { "row": { "field": "r", "type": "nominal", "header": { "labelExpr": "datum.label + datum.value" } } }, "spec": { "mark": "point" } }""";

        // Act / Assert
        Assert.Empty(Visit(json));
        Assert.Empty(Visit(facet));
    }

    /// <summary>
    ///     Validates that an unsupported function or view state in a label expression is reported at the expression with the identifier of the construct, in the guides and in the configuration; and
    ///     that the retired identifier is never produced.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_LabelExpressionConstructsAreReportedAtTheText()
    {
        // Arrange
        const string json = """
            { "config": { "axisBand": { "labelExpr": "luminance(datum.value)" }, "headerColumn": { "labelExpr": "signal" } },
              "mark": "point",
              "encoding": {
                "x": { "field": "a", "type": "quantitative", "axis": { "labelExpr": "now() + datum.label" } },
                "color": { "field": "b", "type": "nominal", "legend": { "labelExpr": "width" } } } }
            """;

        // Act
        var ids = Visit(json);

        // Assert: the views are visited first, then the configuration
        Assert.Contains(("vl-expr-function-now", "/encoding/x/axis/labelExpr"), ids);
        Assert.Contains(("vl-expr-view-state", "/encoding/color/legend/labelExpr"), ids);
        Assert.Contains(("vl-expr-function-luminance", "/config/axisBand/labelExpr"), ids);
        Assert.Contains(("vl-expr-view-state", "/config/headerColumn/labelExpr"), ids);
        Assert.DoesNotContain(ids, i => i.Id == "vl-expr-label");
    }

    /// <summary>
    ///     Validates that a syntax error in a label expression is a specification error when the document is opened, with the location of the text.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="pointer">The pointer of the expression.</param>
    [Theory]
    [InlineData("""{ "mark": "point", "encoding": { "x": { "field": "a", "type": "quantitative", "axis": { "labelExpr": "datum.label +" } } } }""", "/encoding/x/axis/labelExpr")]
    [InlineData("""{ "mark": "point", "encoding": { "color": { "field": "a", "type": "nominal", "legend": { "labelExpr": "(" } } } }""", "/encoding/color/legend/labelExpr")]
    [InlineData("""{ "facet": { "row": { "field": "r", "type": "nominal", "header": { "labelExpr": "1 +" } } }, "spec": { "mark": "point" } }""", "/facet/row/header/labelExpr")]
    [InlineData("""{ "config": { "axis": { "labelExpr": "datum.label +" } }, "mark": "point" }""", "/config/axis/labelExpr")]
    [InlineData("""{ "config": { "headerFacet": { "labelExpr": "1 +" } }, "mark": "point" }""", "/config/headerFacet/labelExpr")]
    [InlineData("""{ "mark": "point", "encoding": { "x": { "field": "a", "type": "quantitative", "axis": { "labelExpr": "datum.label + unknownParam" } } } }""", "/encoding/x/axis/labelExpr")]
    public void SpecParser_Parse_LabelExpressionErrorIsASpecExceptionAtTheText(string json, string pointer)
    {
        // Act
        var error = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse(json));

        // Assert
        Assert.Equal(pointer, error.Location.Pointer);
    }

    /// <summary>
    ///     Validates that the legend configuration is not read: <c>config.legend.labelExpr</c> is neither compiled nor reported (Vega-Lite 6 does not read it).
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_ConfigLegendLabelExprIsIgnored()
    {
        // Act / Assert: the text is not even an expression, and nothing complains
        Assert.Empty(Visit("""{ "config": { "legend": { "labelExpr": "format(datum.value)" } }, "mark": "point" }"""));
    }

    /// <summary>
    ///     Validates the reports of the offset channels: a missing or quantitative position, a time unit that cannot hold an offset (<c>day</c>, <c>weekday</c>, <c>maxbins</c>, binned or on a <c>rect</c>) is reported with <c>vl-offset-position</c> and a continuous offset of a bar or area with
    ///     <c>vl-offset-ranged</c>; a nominal position with a time unit is a band parent like any other and is not reported; a supported offset is not reported, and neither is it by the generic channel report that the offsets had before.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_OffsetsAreReportedOnlyWhenTheyAreNotDrawn()
    {
        // Act
        var supported = Visit(Chart("""{ "field": "c", "type": "nominal" }""").Replace("\"y\":", "\"xOffset\": { \"field\": \"g\", \"type\": \"nominal\" }, \"y\":", StringComparison.Ordinal));
        var quantitative = Visit(Chart("""{ "field": "c", "type": "quantitative" }""").Replace("\"y\":", "\"xOffset\": { \"field\": \"g\", \"type\": \"nominal\" }, \"y\":", StringComparison.Ordinal));
        var unit = Visit(Chart("""{ "field": "c", "type": "temporal", "timeUnit": "year" }""").Replace("\"y\":", "\"xOffset\": { \"field\": \"g\", \"type\": \"nominal\" }, \"y\":", StringComparison.Ordinal));
        var day = Visit(Chart("""{ "field": "c", "type": "temporal", "timeUnit": "day" }""").Replace("\"y\":", "\"xOffset\": { \"field\": \"g\", \"type\": \"nominal\" }, \"y\":", StringComparison.Ordinal));
        var maxbins = Visit(Chart("""{ "field": "c", "type": "temporal", "timeUnit": { "maxbins": 10 } }""").Replace("\"y\":", "\"xOffset\": { \"field\": \"g\", \"type\": \"nominal\" }, \"y\":", StringComparison.Ordinal));
        var rect = Visit(Chart("""{ "field": "c", "type": "temporal", "timeUnit": "year" }""").Replace("\"bar\"", "\"rect\"", StringComparison.Ordinal).Replace("\"y\":", "\"xOffset\": { \"field\": \"g\", \"type\": \"nominal\" }, \"y\":", StringComparison.Ordinal));
        var nominalUnit = Visit(Chart("""{ "field": "c", "type": "nominal", "timeUnit": "year" }""").Replace("\"y\":", "\"xOffset\": { \"field\": \"g\", \"type\": \"nominal\" }, \"y\":", StringComparison.Ordinal));
        var ranged = Visit(Chart("""{ "field": "c", "type": "nominal" }""").Replace("\"y\":", "\"xOffset\": { \"field\": \"g\", \"type\": \"quantitative\" }, \"y\":", StringComparison.Ordinal));
        var nominalRect = Visit(Chart("""{ "field": "c", "type": "nominal", "timeUnit": "year" }""").Replace("\"bar\"", "\"rect\"", StringComparison.Ordinal).Replace("\"y\":", "\"xOffset\": { \"field\": \"g\", \"type\": \"nominal\" }, \"y\":", StringComparison.Ordinal));
        var yOffset = Visit("""{ "mark": "bar", "encoding": { "x": { "field": "v", "type": "quantitative" }, "yOffset": { "field": "g", "type": "nominal" } } }""");

        // Assert
        Assert.Empty(supported);
        Assert.Equal([("vl-offset-position", "/encoding/xOffset")], quantitative);
        Assert.Empty(unit);
        Assert.Equal([("vl-offset-position", "/encoding/xOffset")], day);
        Assert.Equal([("vl-offset-position", "/encoding/xOffset")], maxbins);
        Assert.Equal([("vl-offset-position", "/encoding/xOffset")], rect);
        Assert.Empty(nominalUnit);
        Assert.Empty(nominalRect);
        Assert.Equal([("vl-offset-ranged", "/encoding/xOffset")], ranged);
        Assert.Equal([("vl-offset-position", "/encoding/yOffset")], yOffset);
    }

    /// <summary>
    ///     Validates that an explicit <c>point</c> scale on a position that holds an offset warns that the default (band) scale is used.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_PointScaleOnAPositionWithOffsetWarns()
    {
        // Arrange
        var json = Chart("""{ "field": "c", "type": "nominal", "scale": { "type": "point" } }""").Replace("\"y\":", "\"xOffset\": { \"field\": \"g\", \"type\": \"nominal\" }, \"y\":", StringComparison.Ordinal);

        // Act
        var ids = Visit(json);

        // Assert
        Assert.Equal([("vl-scale-type-ignored", "/encoding/x/scale/type")], ids);
    }

    /// <summary>
    ///     Validates that an explicit <c>point</c> scale on a nominal position with a time unit that holds an offset warns too (the position is a band parent).
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_PointScaleOnADiscreteTimeUnitPositionWithOffsetWarns()
    {
        // Arrange
        var json = Chart("""{ "field": "c", "type": "nominal", "timeUnit": "year", "scale": { "type": "point" } }""").Replace("\"y\":", "\"xOffset\": { \"field\": \"g\", \"type\": \"nominal\" }, \"y\":", StringComparison.Ordinal);

        // Act
        var ids = Visit(json);

        // Assert
        Assert.Equal([("vl-scale-type-ignored", "/encoding/x/scale/type")], ids);
    }

    /// <summary>
    ///     Validates the <c>for</c> of a step size: <c>position</c> and <c>offset</c> are accepted and anything else is a specification error.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_StepForMustBePositionOrOffset()
    {
        // Act
        var position = SpecParser.Parse("""{ "width": { "step": 30, "for": "position" }, "mark": "bar" }""");
        var offset = SpecParser.Parse("""{ "height": { "step": 30, "for": "offset" }, "mark": "bar" }""");
        var error = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse("""{ "width": { "step": 30, "for": "both" }, "mark": "bar" }"""));

        // Assert
        Assert.NotNull(position);
        Assert.NotNull(offset);
        Assert.Equal("/width/for", error.Location.Pointer);
    }
}
