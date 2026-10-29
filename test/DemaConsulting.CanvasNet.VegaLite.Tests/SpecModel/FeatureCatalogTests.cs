using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.SpecModel;

/// <summary>
///     Unit tests for <see cref="FeatureCatalog"/>, the list of recognized-but-unsupported constructs with stable
///     identifiers.
/// </summary>
public class FeatureCatalogTests
{
    /// <summary>
    ///     Parses a specification and lists the findings of the catalog as identifier and pointer pairs.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <returns>The findings in the order the catalog returns them.</returns>
    private static List<(string Id, string Pointer)> Visit(string json) =>
        FeatureCatalog.Visit(SpecParser.Parse(json)).Select(r => (r.Id, r.Location.Pointer)).ToList();

    /// <summary>
    ///     Validates that every recognized-but-unsupported construct of an original specification is reported with its
    ///     stable identifier at the right pointer, in document order, and that supported and ignored constructs are not.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_RecognizedUnsupportedPropertiesReturnsStableIds()
    {
        // Arrange: one construct per line, so that document order is easy to follow; tooltip is accepted silently
        const string json = """
            {
              "$schema": "https://vega.github.io/schema/vega-lite/v4.json",
              "title": { "text": { "expr": "'t'" } },
              "autosize": "fit",
              "params": [
                { "name": "v", "value": 1 },
                { "name": "s", "select": "interval" }
              ],
              "projection": { "type": "mercator" },
              "data": { "url": "sales.csv", "format": { "type": "csv", "parse": { "d": "date" }, "property": "rows" } },
              "transform": [
                { "calculate": "datum.a * 2", "as": "b" },
                { "filter": "datum.b > 1" },
                { "aggregate": [ { "op": "count", "as": "n" } ], "groupby": [ "a" ] }
              ],
              "mark": "arc",
              "encoding": {
                "x": { "field": "d", "type": "temporal", "timeUnit": "year" },
                "y": { "field": "n", "type": "quantitative", "aggregate": "sum", "bin": true, "stack": "zero", "sort": "-x", "condition": { "param": "s", "value": 1 } },
                "color": { "field": "c", "type": "nominal", "scale": { "type": "log", "scheme": "x" }, "legend": { "encode": {} } },
                "theta": { "field": "n", "type": "quantitative" },
                "latitude": { "field": "lat", "type": "quantitative" },
                "tooltip": { "field": "c" }
              },
              "layer": [ { "mark": "geoshape" } ]
            }
            """;

        // Act
        var reports = FeatureCatalog.Visit(SpecParser.Parse(json, "all.json"));

        // Assert: stable identifiers at exact pointers, in document order
        Assert.Equal(
            [
                ("vl-schema-version", "/$schema"),
                ("vl-expr-ref", "/title/text"),
                ("vl-layout-autosize-fit", "/autosize"),
                ("vl-param-variable", "/params/0"),
                ("vl-param-selection", "/params/1"),
                ("vl-projection", "/projection"),
                ("vl-data-file", "/data"),
                ("vl-data-format-csv", "/data/format/type"),
                ("vl-data-parse", "/data/format/parse"),
                ("vl-data-property", "/data/format/property"),
                ("vl-transform-calculate", "/transform/0"),
                ("vl-expr-calculate", "/transform/0/calculate"),
                ("vl-transform-filter", "/transform/1"),
                ("vl-expr-filter", "/transform/1/filter"),
                ("vl-transform-aggregate", "/transform/2"),
                ("vl-mark-arc", "/mark"),
                ("vl-type-temporal", "/encoding/x/type"),
                ("vl-encoding-timeunit", "/encoding/x/timeUnit"),
                ("vl-encoding-aggregate", "/encoding/y/aggregate"),
                ("vl-encoding-bin", "/encoding/y/bin"),
                ("vl-encoding-stack", "/encoding/y/stack"),
                ("vl-encoding-sort", "/encoding/y/sort"),
                ("vl-encoding-condition", "/encoding/y/condition"),
                ("vl-scale-log", "/encoding/color/scale/type"),
                ("vl-scale-property-scheme", "/encoding/color/scale/scheme"),
                ("vl-guide-legend", "/encoding/color/legend"),
                ("vl-guide-encode", "/encoding/color/legend/encode"),
                ("vl-channel-theta", "/encoding/theta"),
                ("vl-channel-geo", "/encoding/latitude"),
                ("vl-composition-layer", "/layer"),
                ("vl-mark-geoshape", "/layer/0/mark"),
            ],
            reports.Select(r => (r.Id, r.Location.Pointer)));

        // Assert: only the schema version is a warning; every other report is an unsupported feature; all carry positions and the file
        Assert.Equal(FeatureReportKind.Warning, reports[0].Kind);
        Assert.All(reports.Skip(1), r => Assert.Equal(FeatureReportKind.UnsupportedFeature, r.Kind));
        Assert.All(reports, r => Assert.True(r.Location.Line >= 1 && r.Location.Column >= 1 && r.Message.Length > 0));
        Assert.All(reports, r => Assert.Equal("all.json", r.Location.File));
        Assert.Equal((2, 14), (reports[0].Location.Line, reports[0].Location.Column));
    }

    /// <summary>
    ///     Validates that a specification that uses only supported constructs produces no reports at all.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_SupportedSpecificationHasNoReports()
    {
        // Arrange: bar and friends, supported channels with field, value and datum forms, supported scale and axis properties
        const string json = """
            {
              "$schema": "https://vega.github.io/schema/vega-lite/v6.json",
              "title": { "text": "Widgets", "subtitle": "per day", "anchor": "start" },
              "width": 300, "height": { "step": 20 }, "padding": 5, "autosize": "pad", "background": "white",
              "data": { "values": [ { "k": "a", "v": 1 }, { "k": "b", "v": 2 } ] },
              "mark": { "type": "bar", "color": "red" },
              "encoding": {
                "x": { "field": "k", "type": "nominal", "scale": { "type": "band", "padding": 0.2 }, "axis": { "labelAngle": 0, "grid": false, "title": null }, "sort": "descending" },
                "y": { "field": "v", "type": "quantitative", "stack": null, "scale": { "zero": true, "nice": true, "domain": [0, 10], "range": [100, 0] }, "axis": { "tickCount": 5, "format": ".1f" } },
                "color": { "value": "steelblue" },
                "opacity": { "datum": 0.5 },
                "size": { "value": 5, "legend": null },
                "fill": { "field": "k", "type": "nominal", "legend": null },
                "stroke": { "field": "k", "type": "nominal", "scale": null },
                "tooltip": { "field": "v" }, "href": { "value": "x" }, "description": { "value": "d" }, "key": { "field": "k" }
              }
            }
            """;

        // Act
        var reports = Visit(json);

        // Assert
        Assert.Empty(reports);
    }

    /// <summary>
    ///     Validates that remote data (every scheme-bearing form) reports <c>vl-data-remote-url</c> at the data object and
    ///     a relative local path reports <c>vl-data-file</c>.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_RemoteDataReportsVlDataRemoteUrl()
    {
        // Arrange: every scheme the contract names, a UNC path and a drive letter, then local relative paths
        string[] remote =
        [
            "http://example.org/a.csv", "https://example.org/a.csv", "ftp://host/a.csv", "file:///etc/hosts",
            "data:text/csv,a", "javascript:alert(1)", "//server/share/a.csv", "\\\\server\\share\\a.csv", "C:\\data\\a.csv",
            "/etc/hosts", "/a.csv", "\\windows\\system32\\a.csv", "C:a.csv", "d:/data/a.csv", "file:///etc/hosts", "file:/etc/hosts", "FILE://host/a.csv",
        ];
        string[] local = ["a.csv", "data/a.csv", "../a.csv"];
        var layers = string.Join(",", remote.Concat(local).Select(u => "{\"data\":{\"url\":" + System.Text.Json.JsonSerializer.Serialize(u) + "}}"));

        // Act
        var reports = FeatureCatalog.Visit(SpecParser.Parse("{\"layer\":[" + layers + "]}"));

        // Assert: remote urls first (they come first in the text), each at its data object and never a warning
        var remoteReports = reports.Where(r => r.Id == "vl-data-remote-url").ToList();
        Assert.Equal(remote.Length, remoteReports.Count);
        Assert.Equal(
            Enumerable.Range(0, remote.Length).Select(i => $"/layer/{i}/data"),
            remoteReports.Select(r => r.Location.Pointer));
        Assert.All(remoteReports, r => Assert.Equal(FeatureReportKind.UnsupportedFeature, r.Kind));
        Assert.Contains("never fetched", remoteReports[0].Message, StringComparison.Ordinal);

        // Assert: local relative urls are a different, deferred feature
        var fileReports = reports.Where(r => r.Id == "vl-data-file").ToList();
        Assert.Equal(
            Enumerable.Range(remote.Length, local.Length).Select(i => $"/layer/{i}/data"),
            fileReports.Select(r => r.Location.Pointer));
    }

    /// <summary>
    ///     Validates the url classification of remote data.
    /// </summary>
    /// <param name="url">The URL as written.</param>
    /// <param name="expected">Whether the URL is scheme-bearing, a network path or a drive path.</param>
    [Theory]
    [InlineData("http://example.org/a.csv", true)]
    [InlineData("HTTPS://example.org/a.csv", true)]
    [InlineData("file:///etc/passwd", true)]
    [InlineData("data:text/csv,a", true)]
    [InlineData("javascript:alert(1)", true)]
    [InlineData("ftp://host/a.csv", true)]
    [InlineData("C:\\data\\a.csv", true)]
    [InlineData("//server/share/a.csv", true)]
    [InlineData("\\\\server\\share\\a.csv", true)]
    [InlineData("/etc/hosts", true)]
    [InlineData("/", true)]
    [InlineData("  /etc/hosts", true)]
    [InlineData("\\windows\\a.csv", true)]
    [InlineData("C:data.csv", true)]
    [InlineData("d:/data/a.csv", true)]
    [InlineData("file:/etc/hosts", true)]
    [InlineData("FILE://host/share/a.csv", true)]
    [InlineData("  http://example.org/a.csv", true)]
    [InlineData("a.csv", false)]
    [InlineData("data/a.csv", false)]
    [InlineData("../a.csv", false)]
    [InlineData("a b.csv", false)]
    [InlineData("", false)]
    [InlineData("1:2.csv", false)]
    public void FeatureCatalog_IsSchemeBearingUrl_ClassifiesUrls(string url, bool expected)
    {
        // Arrange / Act
        var result = FeatureCatalog.IsSchemeBearingUrl(url);

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>
    ///     Validates that every mark type except the supported ones reports <c>vl-mark-&lt;type&gt;</c> at the mark.
    /// </summary>
    /// <param name="type">The mark type.</param>
    /// <param name="supported">Whether the renderer draws the mark.</param>
    [Theory]
    [InlineData("bar", true)]
    [InlineData("rect", true)]
    [InlineData("point", true)]
    [InlineData("circle", true)]
    [InlineData("square", true)]
    [InlineData("rule", true)]
    [InlineData("tick", true)]
    [InlineData("arc", false)]
    [InlineData("area", false)]
    [InlineData("image", false)]
    [InlineData("line", false)]
    [InlineData("text", false)]
    [InlineData("trail", false)]
    [InlineData("geoshape", false)]
    [InlineData("boxplot", false)]
    [InlineData("errorbar", false)]
    [InlineData("errorband", false)]
    public void FeatureCatalog_Visit_UnsupportedMarkReportsMarkId(string type, bool supported)
    {
        // Arrange: the same mark written as a string and as a mark definition object, in two layers
        var json = "{\"layer\":[{\"mark\":\"" + type + "\"},{\"mark\":{\"type\":\"" + type + "\",\"color\":\"red\"}}]}";

        // Act
        var reports = Visit(json).Where(r => r.Id.StartsWith("vl-mark-", StringComparison.Ordinal)).ToList();

        // Assert
        if (supported)
        {
            Assert.Empty(reports);
        }
        else
        {
            Assert.Equal([("vl-mark-" + type, "/layer/0/mark"), ("vl-mark-" + type, "/layer/1/mark")], reports);
        }
    }

    /// <summary>
    ///     Validates the report location of a mark: the value of the <c>mark</c> property.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_MarkReportCarriesLineAndColumn()
    {
        // Arrange
        const string json = "{\n  \"mark\": \"arc\"\n}";

        // Act
        var report = Assert.Single(FeatureCatalog.Visit(SpecParser.Parse(json, "m.json")));

        // Assert
        Assert.Equal("vl-mark-arc", report.Id);
        Assert.Equal(new VegaLiteLocation("m.json", "/mark", 2, 11), report.Location);
    }

    /// <summary>
    ///     Validates which channels are drawn, which are accepted silently, and which report
    ///     <c>vl-channel-&lt;name&gt;</c>, with the geographic channels sharing one identifier.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_ChannelsReportExceptSupportedAndIgnored()
    {
        // Arrange
        const string json = """
            { "mark": "point", "encoding": {
              "x": {"field":"a","type":"quantitative"}, "y": {"field":"a","type":"quantitative"},
              "color": {"value":"red"}, "fill": {"value":"red"}, "stroke": {"value":"red"}, "size": {"value":3}, "opacity": {"value":1},
              "tooltip": {"value":"t"}, "href": {"value":"h"}, "description": {"value":"d"}, "key": {"field":"a"},
              "x2": {"field":"a"}, "shape": {"field":"a","type":"nominal"}, "strokeWidth": {"value":2}, "text": {"field":"a"},
              "detail": [ {"field":"a"} ], "order": {"field":"a"}, "row": {"field":"a"}, "xOffset": {"field":"a"},
              "longitude": {"field":"a"}, "latitude2": {"field":"a"}
            } }
            """;

        // Act
        var ids = Visit(json);

        // Assert
        Assert.Equal(
            [
                ("vl-channel-x2", "/encoding/x2"),
                ("vl-channel-shape", "/encoding/shape"),
                ("vl-channel-strokewidth", "/encoding/strokeWidth"),
                ("vl-channel-text", "/encoding/text"),
                ("vl-channel-detail", "/encoding/detail"),
                ("vl-channel-order", "/encoding/order"),
                ("vl-channel-row", "/encoding/row"),
                ("vl-channel-xoffset", "/encoding/xOffset"),
                ("vl-channel-geo", "/encoding/longitude"),
                ("vl-channel-geo", "/encoding/latitude2"),
            ],
            ids);
    }

    /// <summary>
    ///     Validates that unknown channels are only warned about by the parser, not reported again by the catalog.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_UnknownChannelIsNotReported()
    {
        // Arrange
        var tree = SpecParser.Parse("{\"mark\":\"bar\",\"encoding\":{\"hue\":{\"field\":\"a\"}}}");

        // Act
        var reports = FeatureCatalog.Visit(tree);

        // Assert
        Assert.Empty(reports);
        Assert.Equal("vl-unknown-property", Assert.Single(tree.Warnings).Id);
    }

    /// <summary>
    ///     Validates the schema version rules: 5 and 6 are silent, other versions and non-Vega-Lite URLs warn.
    /// </summary>
    /// <param name="schema">The <c>$schema</c> text.</param>
    /// <param name="warns">Whether a warning is expected.</param>
    [Theory]
    [InlineData("https://vega.github.io/schema/vega-lite/v5.json", false)]
    [InlineData("https://vega.github.io/schema/vega-lite/v6.json", false)]
    [InlineData("https://vega.github.io/schema/vega-lite/v5.17.0.json", false)]
    [InlineData("https://vega.github.io/schema/vega-lite/v4.json", true)]
    [InlineData("https://vega.github.io/schema/vega-lite/v7.json", true)]
    [InlineData("https://example.org/chart.json", true)]
    public void FeatureCatalog_Visit_SchemaVersionWarnsOutsideFiveAndSix(string schema, bool warns)
    {
        // Arrange / Act
        var reports = FeatureCatalog.Visit(SpecParser.Parse("{\"$schema\":\"" + schema + "\"}"));

        // Assert
        if (warns)
        {
            var report = Assert.Single(reports);
            Assert.Equal("vl-schema-version", report.Id);
            Assert.Equal(FeatureReportKind.Warning, report.Kind);
            Assert.Equal("/$schema", report.Location.Pointer);
        }
        else
        {
            Assert.Empty(reports);
        }
    }

    /// <summary>
    ///     Validates the data reports: empty or sourceless inline data warns, and each deferred source kind has its own identifier.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_DataSourcesReportTheirOwnIdentifiers()
    {
        // Arrange: one data object per layer
        const string json = """
            { "layer": [
              { "data": { "values": [] } },
              { "data": { "values": [ {"a":1} ] } },
              { "data": { "format": {} } },
              { "data": { "name": "t" } },
              { "data": { "name": "t", "values": [ {"a":1} ] } },
              { "data": { "sequence": { "start": 0, "stop": 3 } } },
              { "data": { "values": "a,b" } },
              { "data": { "values": { "type": "Topology" } } },
              { "data": { "sphere": true } },
              { "data": { "values": [ {"a":1} ], "format": { "type": "topojson" } } },
              { "data": null }
            ] }
            """;

        // Act
        var reports = FeatureCatalog.Visit(SpecParser.Parse(json)).Where(r => r.Id != "vl-composition-layer").ToList();

        // Assert
        Assert.Equal(
            [
                ("vl-data-empty", "/layer/0/data", FeatureReportKind.Warning),
                ("vl-data-empty", "/layer/2/data", FeatureReportKind.Warning),
                ("vl-data-named", "/layer/3/data", FeatureReportKind.UnsupportedFeature),
                ("vl-data-sequence", "/layer/5/data/sequence", FeatureReportKind.UnsupportedFeature),
                ("vl-data-values-text", "/layer/6/data/values", FeatureReportKind.UnsupportedFeature),
                ("vl-data-geo", "/layer/7/data/values", FeatureReportKind.UnsupportedFeature),
                ("vl-data-geo", "/layer/8/data/sphere", FeatureReportKind.UnsupportedFeature),
                ("vl-data-geo", "/layer/9/data/format/type", FeatureReportKind.UnsupportedFeature),
            ],
            reports.Select(r => (r.Id, r.Location.Pointer, r.Kind)));
    }

    /// <summary>
    ///     Validates the encoding feature rules: what each of the encoding identifiers is reported for, and what is not.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_EncodingPropertiesReportOnlyWhenTheyChangeTheChart()
    {
        // Arrange: harmless forms first (switched off or neutral), then the reported forms
        const string json = """
            { "mark": "point", "encoding": {
              "x": { "field": "a", "type": "quantitative", "bin": false, "stack": false, "sort": "ascending" },
              "y": { "field": "a", "type": "quantitative", "bin": null, "stack": null, "sort": null, "aggregate": null, "timeUnit": null, "impute": null },
              "color": { "field": "a", "type": "ordinal", "sort": [ "b", "a" ], "legend": null, "bin": "binned", "stack": true, "sort2": 1 },
              "size": { "field": "a", "type": "nominal", "sort": { "op": "min", "field": "b" }, "impute": { "value": 0 }, "bandPosition": 0.5, "legend": null }
            } }
            """;

        // Act
        var ids = Visit(json);

        // Assert
        Assert.Equal(
            [
                ("vl-encoding-bin", "/encoding/color/bin"),
                ("vl-encoding-stack", "/encoding/color/stack"),
                ("vl-encoding-sort", "/encoding/size/sort"),
                ("vl-encoding-impute", "/encoding/size/impute"),
                ("vl-encoding-bandposition", "/encoding/size/bandPosition"),
            ],
            ids);
    }

    /// <summary>
    ///     Validates the legend rule: a legend is reported for field-driven color, size and opacity channels unless it
    ///     or the scale is null, never for values, data and position channels.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_LegendsAreReportedOnlyWhenOneWouldBeDrawn()
    {
        // Arrange
        const string json = """
            { "mark": "point", "encoding": {
              "color": { "field": "a", "type": "nominal" },
              "size": { "field": "a", "type": "quantitative", "legend": { "orient": "left" } },
              "opacity": { "field": "a", "type": "quantitative", "legend": null },
              "fill": { "field": "a", "type": "nominal", "scale": null },
              "stroke": { "value": "red" },
              "x": { "field": "a", "type": "nominal", "legend": {} }
            } }
            """;

        // Act
        var ids = Visit(json);

        // Assert: color has no legend object, so the channel itself is the location; size reports its legend object
        Assert.Equal([("vl-guide-legend", "/encoding/color"), ("vl-guide-legend", "/encoding/size/legend")], ids);
    }

    /// <summary>
    ///     Validates the scale rules: unsupported types and properties report, the supported subset does not.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_ScaleTypesAndPropertiesReportOutsideTheSupportedSubset()
    {
        // Arrange
        const string json = """
            { "mark": "point", "encoding": {
              "x": { "field": "a", "type": "quantitative", "scale": { "type": "linear", "zero": false, "nice": true, "padding": 1, "domain": [0, 1], "range": [0, 1] } },
              "y": { "field": "a", "type": "nominal", "scale": { "type": "point", "paddingInner": 0.1, "clamp": true, "exponent": 2 } },
              "size": { "field": "a", "type": "ordinal", "scale": { "type": "ordinal" }, "legend": null },
              "opacity": { "value": 1, "scale": { "type": "bin-ordinal", "reverse": true } },
              "stroke": { "value": "x", "scale": { "type": "sqrt" } },
              "fill": { "value": "x", "scale": { "type": "band" } }
            } }
            """;

        // Act
        var ids = Visit(json);

        // Assert
        Assert.Equal(
            [
                ("vl-scale-property-exponent", "/encoding/y/scale/exponent"),
                ("vl-scale-bin-ordinal", "/encoding/opacity/scale/type"),
                ("vl-scale-sqrt", "/encoding/stroke/scale/type"),
            ],
            ids);
    }

    /// <summary>
    ///     Validates that a color scheme other than the default palette and a continuous color scale are reported, and the default
    ///     palette and an ordinal color scale are not.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_ColorSchemesAndContinuousColorsReportOutsideTheSupportedSubset()
    {
        // Arrange
        const string json = """
            { "mark": "point", "encoding": {
              "color": { "field": "a", "type": "quantitative", "legend": null },
              "fill": { "field": "b", "type": "nominal", "scale": { "scheme": "tableau10" }, "legend": null },
              "stroke": { "field": "c", "type": "nominal", "scale": { "scheme": { "name": "category20" } }, "legend": null },
              "opacity": { "field": "d", "type": "ordinal", "scale": { "scheme": "blues" }, "legend": null }
            } }
            """;

        // Act
        var ids = Visit(json);

        // Assert: the quantitative color has no ramp; tableau10 (also as an object) is available, other schemes are not
        Assert.Equal(
            [
                ("vl-scale-color-ramp", "/encoding/color/type"),
                ("vl-scale-property-scheme", "/encoding/stroke/scale/scheme"),
                ("vl-scale-property-scheme", "/encoding/opacity/scale/scheme"),
            ],
            ids);
    }

    /// <summary>
    ///     Validates that the scheme report agrees with the scale resolver: the name is matched without regard to case, an
    ///     explicit color range overrides the scheme (so nothing is reported), and each report is made once with its pointer.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_SchemeReportAgreesWithTheResolver()
    {
        // Arrange
        const string json = """
            { "mark": "point", "encoding": {
              "color": { "field": "a", "type": "nominal", "scale": { "scheme": "Tableau10" }, "legend": null },
              "fill": { "field": "b", "type": "nominal", "scale": { "scheme": "blues", "range": ["red", "blue"] }, "legend": null },
              "stroke": { "field": "c", "type": "nominal", "scale": { "scheme": "BLUES" }, "legend": null }
            } }
            """;

        // Act
        var reports = FeatureCatalog.Visit(SpecParser.Parse(json));

        // Assert: only the unavailable scheme without a range is reported
        var report = Assert.Single(reports);
        Assert.Equal(("vl-scale-property-scheme", "/encoding/stroke/scale/scheme"), (report.Id, report.Location.Pointer));
        Assert.Contains("'BLUES'", report.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the axis rules: guide encodes, expressions and custom formats, and the listed unsupported properties.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_AxisPropertiesReportOnlyTheUnsupportedOnes()
    {
        // Arrange
        const string json = """
            { "mark": "point", "encoding": {
              "x": { "field": "a", "type": "quantitative", "axis": { "grid": true, "tickCount": 3, "format": ".2f", "title": "T", "orient": "top", "labelOverlap": "parity", "tickMinStep": 2, "labelFlush": true, "labelBound": true, "tickBand": "extent" } },
              "y": { "field": "a", "type": "quantitative", "axis": { "encode": { "labels": {} }, "labelExpr": "datum.label", "formatType": "custom", "position": 5 } }
            } }
            """;

        // Act
        var ids = Visit(json);

        // Assert
        Assert.Equal(
            [
                ("vl-guide-axis-labelbound", "/encoding/x/axis/labelBound"),
                ("vl-guide-axis-tickband", "/encoding/x/axis/tickBand"),
                ("vl-guide-encode", "/encoding/y/axis/encode"),
                ("vl-expr-label", "/encoding/y/axis/labelExpr"),
                ("vl-format-type-custom", "/encoding/y/axis/formatType"),
                ("vl-guide-axis-position", "/encoding/y/axis/position"),
            ],
            ids);
    }

    /// <summary>
    ///     Validates the implicit stacking report: bars that Vega-Lite would stack by default are reported at the
    ///     measure, and charts that it would draw unstacked are not.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="expectedPointer">The pointer of the report, or null when there must be none.</param>
    [Theory]
    [InlineData("{\"mark\":\"bar\",\"encoding\":{\"x\":{\"field\":\"k\",\"type\":\"nominal\"},\"y\":{\"field\":\"v\",\"type\":\"quantitative\"},\"color\":{\"field\":\"g\",\"type\":\"nominal\",\"legend\":null}}}", "/encoding/y")]
    [InlineData("{\"mark\":{\"type\":\"bar\"},\"encoding\":{\"y\":{\"field\":\"k\",\"type\":\"ordinal\"},\"x\":{\"field\":\"v\",\"type\":\"quantitative\"},\"detail\":[{\"field\":\"g\"}]}}", "/encoding/x")]
    [InlineData("{\"mark\":\"bar\",\"encoding\":{\"y\":{\"field\":\"v\",\"type\":\"quantitative\"},\"size\":{\"field\":\"g\",\"type\":\"nominal\",\"legend\":null}}}", "/encoding/y")]
    [InlineData("{\"mark\":\"bar\",\"encoding\":{\"x\":{\"field\":\"k\",\"type\":\"nominal\"},\"y\":{\"field\":\"v\",\"type\":\"quantitative\",\"stack\":null},\"color\":{\"field\":\"g\",\"type\":\"nominal\",\"legend\":null}}}", null)]
    [InlineData("{\"mark\":\"bar\",\"encoding\":{\"x\":{\"field\":\"k\",\"type\":\"nominal\"},\"y\":{\"field\":\"v\",\"type\":\"quantitative\"},\"color\":{\"field\":\"k\",\"type\":\"nominal\",\"legend\":null}}}", null)]
    [InlineData("{\"mark\":\"bar\",\"encoding\":{\"x\":{\"field\":\"k\",\"type\":\"nominal\"},\"y\":{\"field\":\"v\",\"type\":\"quantitative\"},\"y2\":{\"field\":\"w\"},\"color\":{\"field\":\"g\",\"type\":\"nominal\",\"legend\":null}}}", null)]
    [InlineData("{\"mark\":\"point\",\"encoding\":{\"x\":{\"field\":\"k\",\"type\":\"nominal\"},\"y\":{\"field\":\"v\",\"type\":\"quantitative\"},\"color\":{\"field\":\"g\",\"type\":\"nominal\",\"legend\":null}}}", null)]
    [InlineData("{\"mark\":\"bar\",\"encoding\":{\"x\":{\"field\":\"k\",\"type\":\"nominal\"},\"y\":{\"field\":\"v\",\"type\":\"quantitative\"},\"color\":{\"value\":\"red\"}}}", null)]
    [InlineData("{\"mark\":\"bar\",\"encoding\":{\"x\":{\"field\":\"k\",\"type\":\"quantitative\"},\"y\":{\"field\":\"v\",\"type\":\"quantitative\"},\"color\":{\"field\":\"g\",\"type\":\"nominal\",\"legend\":null}}}", null)]
    public void FeatureCatalog_Visit_ImplicitBarStackIsReportedAtTheMeasure(string json, string? expectedPointer)
    {
        // Arrange / Act
        var stacks = Visit(json).Where(r => r.Id == "vl-encoding-stack").ToList();

        // Assert
        if (expectedPointer is null)
        {
            Assert.Empty(stacks);
        }
        else
        {
            Assert.Equal([("vl-encoding-stack", expectedPointer)], stacks);
        }
    }

    /// <summary>
    ///     Validates the report of transforms, expressions and parameters, including expression references anywhere in
    ///     the view's properties and the silent handling of <c>bind</c>.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_TransformsExpressionsAndParametersAreReported()
    {
        // Arrange
        const string json = """
            {
              "width": { "expr": "w" },
              "params": [ { "name": "a", "bind": { "input": "range" }, "value": 1 }, { "name": "b", "select": { "type": "point" } } ],
              "transform": [ { "filter": { "field": "x", "equal": 1 } }, { "timeUnit": "month", "field": "d", "as": "m" }, { "joinaggregate": [], "groupby": [] } ],
              "mark": { "type": "bar", "size": { "expr": "3" } },
              "encoding": { "x": { "field": "a", "type": "nominal" }, "color": { "value": { "expr": "'red'" } } }
            }
            """;

        // Act
        var ids = Visit(json);

        // Assert: the filter object has no expression of its own; bind is ignored
        Assert.Equal(
            [
                ("vl-expr-ref", "/width"),
                ("vl-param-variable", "/params/0"),
                ("vl-param-selection", "/params/1"),
                ("vl-transform-filter", "/transform/0"),
                ("vl-transform-timeunit", "/transform/1"),
                ("vl-transform-joinaggregate", "/transform/2"),
                ("vl-expr-ref", "/mark/size"),
                ("vl-expr-ref", "/encoding/color/value"),
            ],
            ids);
    }

    /// <summary>
    ///     Validates that every composition is reported and that the views nested in it are examined too, at any depth.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_CompositionsAreReportedAndNestedViewsExamined()
    {
        // Arrange
        const string json = """
            {
              "hconcat": [ { "mark": "arc" }, { "vconcat": [ { "mark": "text" } ] } ],
              "concat": [], "facet": { "row": { "field": "a" } }, "spec": { "mark": "line" }, "repeat": [ "a" ],
              "resolve": { "scale": { "x": "independent" } }, "projection": { "type": "albers" }
            }
            """;

        // Act
        var ids = Visit(json);

        // Assert
        Assert.Equal(
            [
                ("vl-composition-hconcat", "/hconcat"),
                ("vl-mark-arc", "/hconcat/0/mark"),
                ("vl-composition-vconcat", "/hconcat/1/vconcat"),
                ("vl-mark-text", "/hconcat/1/vconcat/0/mark"),
                ("vl-composition-concat", "/concat"),
                ("vl-composition-facet", "/facet"),
                ("vl-mark-line", "/spec/mark"),
                ("vl-composition-repeat", "/repeat"),
                ("vl-projection", "/projection"),
            ],
            ids);
    }

    /// <summary>
    ///     Validates that the autosize types other than pad and none are reported at the type.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="expected">The expected identifier, or null for none.</param>
    [Theory]
    [InlineData("{\"autosize\":\"pad\"}", null)]
    [InlineData("{\"autosize\":\"none\"}", null)]
    [InlineData("{\"autosize\":{\"type\":\"pad\",\"resize\":true}}", null)]
    [InlineData("{\"autosize\":\"fit\"}", "vl-layout-autosize-fit")]
    [InlineData("{\"autosize\":\"fit-x\"}", "vl-layout-autosize-fit-x")]
    [InlineData("{\"autosize\":{\"type\":\"fit-y\"}}", "vl-layout-autosize-fit-y")]
    public void FeatureCatalog_Visit_AutosizeFitModesAreReported(string json, string? expected)
    {
        // Arrange / Act
        var ids = Visit(json).Select(r => r.Id).ToList();

        // Assert
        Assert.Equal(expected is null ? [] : new List<string> { expected }, ids);
    }

    /// <summary>
    ///     Validates that reports are sorted by position in the text, not by the order in which the constructs are visited.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_ReportsAreInDocumentOrder()
    {
        // Arrange: the encoding is written before the mark, and a nested view before the parent's later properties
        const string json = "{\"encoding\":{\"shape\":{\"field\":\"a\"}},\"layer\":[{\"mark\":\"arc\"}],\"mark\":\"text\"}";

        // Act
        var ids = Visit(json);

        // Assert
        Assert.Equal(
            [("vl-channel-shape", "/encoding/shape"), ("vl-composition-layer", "/layer"), ("vl-mark-arc", "/layer/0/mark"), ("vl-mark-text", "/mark")],
            ids);
    }
}
