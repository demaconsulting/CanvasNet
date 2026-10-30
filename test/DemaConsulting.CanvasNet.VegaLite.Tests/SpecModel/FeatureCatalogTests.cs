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
              "data": { "url": "sales.csv", "format": { "type": "csv", "parse": { "d": "date:'%d/%m/%Y'", "n": "number" } } },
              "transform": [
                { "calculate": "datum.a * 2", "as": "b" },
                { "filter": "datum.b > 1" },
                { "regression": "n", "on": "a" }
              ],
              "mark": "arc",
              "encoding": {
                "x": { "field": "d", "type": "temporal", "timeUnit": "year" },
                "y": { "field": "n", "type": "quantitative", "aggregate": "sum", "bin": true, "stack": "zero", "sort": "-x", "condition": { "param": "s", "value": 1 } },
                "color": { "field": "c", "type": "nominal", "bin": true, "scale": { "type": "log", "scheme": "x" }, "legend": { "encode": {} } },
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
                ("vl-param-selection", "/params/1"),
                ("vl-projection", "/projection"),
                ("vl-data-parse-date-format", "/data/format/parse/d"),
                ("vl-transform-calculate", "/transform/0"),
                ("vl-transform-filter", "/transform/1"),
                ("vl-transform-regression", "/transform/2"),
                ("vl-mark-arc", "/mark"),
                ("vl-type-temporal", "/encoding/x/type"),
                ("vl-encoding-timeunit", "/encoding/x/timeUnit"),
                ("vl-encoding-sort-ignored", "/encoding/y/sort"),
                ("vl-encoding-condition", "/encoding/y/condition"),
                ("vl-encoding-bin", "/encoding/color/bin"),
                ("vl-scale-log", "/encoding/color/scale/type"),
                ("vl-guide-encode", "/encoding/color/legend/encode"),
                ("vl-channel-theta", "/encoding/theta"),
                ("vl-channel-geo", "/encoding/latitude"),
                ("vl-composition-layer", "/layer"),
                ("vl-mark-geoshape", "/layer/0/mark"),
            ],
            reports.Select(r => (r.Id, r.Location.Pointer)));

        // Assert: only the schema version and the ignored sort are warnings; every other report is an unsupported feature; all carry positions and the file
        Assert.Equal(["vl-schema-version", "vl-encoding-sort-ignored"], reports.Where(r => r.Kind == FeatureReportKind.Warning).Select(r => r.Id));
        Assert.All(reports.Where(r => r.Id is not ("vl-schema-version" or "vl-encoding-sort-ignored")), r => Assert.Equal(FeatureReportKind.UnsupportedFeature, r.Kind));
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
    ///     Validates that remote data (every scheme-bearing form) reports <c>vl-data-remote-url</c> at the data object
    ///     and that a relative local path reports nothing.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_RemoteUrlsRemainVlDataRemoteUrl()
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

        // Assert: local relative urls are supported and report nothing (only the composition itself is reported besides the remote urls)
        Assert.All(reports, r => Assert.True(r.Id is "vl-data-remote-url" or "vl-composition-layer", r.Id));
    }

    /// <summary>
    ///     Validates that relative local urls, the file formats, parse maps, JSON properties, named datasets and sequences
    ///     are supported and report nothing, so that strict mode accepts them.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_LocalUrlsAndPhase4FormatsAreSupported()
    {
        // Arrange: each supported Phase 4 source form in its own layer
        const string json = """
            { "layer": [
              { "data": { "url": "a.csv" } },
              { "data": { "url": "dir/a.tsv", "format": { "type": "tsv" } } },
              { "data": { "url": "a.txt", "format": { "type": "dsv", "delimiter": ";" } } },
              { "data": { "url": "a.json", "format": { "type": "json", "property": "rows.items", "parse": { "d": "date", "n": "number", "b": "boolean", "s": "string" } } } },
              { "data": { "url": "a.csv", "format": { "parse": "auto" } } },
              { "data": { "url": "a.csv", "format": { "parse": null } } },
              { "data": { "name": "t" } },
              { "data": { "sequence": { "start": 0, "stop": 3, "step": 1, "as": "x" } } }
            ] }
            """;

        // Act
        var reports = FeatureCatalog.Visit(SpecParser.Parse(json)).Where(r => r.Id != "vl-composition-layer").ToList();

        // Assert
        Assert.Empty(reports);
    }

    /// <summary>
    ///     Validates that a custom date format of a parse map reports <c>vl-data-parse-date-format</c> at the entry (it
    ///     is not applied) and that plain <c>date</c> does not.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_CustomDateFormatReportsStableIdentifier()
    {
        // Arrange
        const string json = """
            { "data": { "url": "a.csv", "format": { "parse": {
              "a": "date", "b": "date:'%d/%m/%Y'", "c": "utc:'%Y%m%d'", "d": "number"
            } } } }
            """;

        // Act
        var reports = FeatureCatalog.Visit(SpecParser.Parse(json, "dates.json"));

        // Assert
        Assert.Equal(
            [("vl-data-parse-date-format", "/data/format/parse/b"), ("vl-data-parse-date-format", "/data/format/parse/c")],
            reports.Select(r => (r.Id, r.Location.Pointer)));
        Assert.All(reports, r => Assert.Equal(FeatureReportKind.UnsupportedFeature, r.Kind));
        Assert.All(reports, r => Assert.Equal("dates.json", r.Location.File));
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
              "x2": {"field":"a"}, "shape": {"field":"a","type":"nominal","legend":null}, "strokeWidth": {"value":2}, "text": {"field":"a"},
              "detail": [ {"field":"a"} ], "order": {"field":"a"}, "row": {"field":"a"}, "xOffset": {"field":"a"},
              "longitude": {"field":"a"}, "latitude2": {"field":"a"},
              "strokeDash": {"value":[1,1]}, "fillOpacity": {"value":0.5}, "strokeOpacity": {"value":0.5}, "angle": {"value":45}
            } }
            """;

        // Act
        var ids = Visit(json);

        // Assert: shape, strokeWidth, strokeDash, fillOpacity, strokeOpacity and angle are drawn by a point mark
        Assert.Equal(
            [
                ("vl-channel-x2", "/encoding/x2"),
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
    ///     Validates the data reports: empty or sourceless inline data warns, text and object data have their own identifiers,
    ///     and the supported sources (files, named datasets, sequences) are not reported.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_DataSourcesReportTheirOwnIdentifiers()
    {
        // Arrange: one data object per layer
        const string json = """
            { "datasets": { "txt": "a,b\n1,2", "geo": { "type": "Topology" }, "rows": [ {"a":1} ] },
              "layer": [
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
              { "data": null },
              { "data": { "name": "txt" } },
              { "data": { "name": "geo" } },
              { "data": { "name": "rows" } }
            ] }
            """;

        // Act
        var reports = FeatureCatalog.Visit(SpecParser.Parse(json)).Where(r => r.Id != "vl-composition-layer").ToList();

        // Assert
        Assert.Equal(
            [
                ("vl-data-empty", "/layer/0/data", FeatureReportKind.Warning),
                ("vl-data-empty", "/layer/2/data", FeatureReportKind.Warning),
                ("vl-data-values-text", "/layer/6/data/values", FeatureReportKind.UnsupportedFeature),
                ("vl-data-geo", "/layer/7/data/values", FeatureReportKind.UnsupportedFeature),
                ("vl-data-geo", "/layer/8/data/sphere", FeatureReportKind.UnsupportedFeature),
                ("vl-data-geo", "/layer/9/data/format/type", FeatureReportKind.UnsupportedFeature),
                ("vl-data-values-text", "/layer/11/data", FeatureReportKind.UnsupportedFeature),
                ("vl-data-geo", "/layer/12/data", FeatureReportKind.UnsupportedFeature),
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
              "size": { "field": "a", "type": "nominal", "sort": { "encoding": "x", "order": "descending" }, "impute": { "value": 0 }, "bandPosition": 0.5, "legend": null }
            } }
            """;

        // Act
        var ids = Visit(json);

        // Assert
        Assert.Equal(
            [
                ("vl-encoding-bin", "/encoding/color/bin"),
                ("vl-encoding-impute", "/encoding/size/impute"),
                ("vl-encoding-bandposition", "/encoding/size/bandPosition"),
            ],
            ids);
    }

    /// <summary>
    ///     Validates the legend rule: legends are drawn, so they are not reported; only the parts of a legend that are not drawn are
    ///     (a title orient other than top, a clip height, an encode, a label expression, a custom format type, and a custom symbol path),
    ///     at the exact property, and never for a position channel or a channel without a legend.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_LegendsAreDrawnAndOnlyTheirUnsupportedPropertiesAreReported()
    {
        // Arrange
        const string json = """
            { "mark": "point", "encoding": {
              "color": { "field": "a", "type": "nominal", "legend": { "titleOrient": "left", "clipHeight": 20, "labelExpr": "datum.label", "formatType": "time", "symbolType": "M0,0L1,1", "encode": {} } },
              "size": { "field": "a", "type": "quantitative", "legend": { "orient": "left", "titleOrient": "top", "symbolType": "diamond", "title": "S" } },
              "opacity": { "field": "a", "type": "quantitative", "legend": null },
              "fill": { "field": "a", "type": "nominal", "scale": null },
              "stroke": { "field": "a", "type": "nominal" },
              "x": { "field": "a", "type": "nominal", "legend": { "titleOrient": "left" } }
            } }
            """;

        // Act
        var ids = Visit(json);

        // Assert: in document order; the legend of the size channel and the plain ones draw without a report
        Assert.Equal(
            [
                ("vl-guide-legend-titleorient", "/encoding/color/legend/titleOrient"),
                ("vl-guide-legend-clipheight", "/encoding/color/legend/clipHeight"),
                ("vl-expr-label", "/encoding/color/legend/labelExpr"),
                ("vl-format-type-custom", "/encoding/color/legend/formatType"),
                ("vl-shape-path", "/encoding/color/legend/symbolType"),
                ("vl-guide-encode", "/encoding/color/legend/encode"),
            ],
            ids);
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
    ///     Validates that continuous color scales, the color schemes (of every kind) and the other color scale properties of Phase 7 are not
    ///     reported, and that the retired <c>vl-scale-color-ramp</c> never is.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_ContinuousColorsAndSchemesAreSupported()
    {
        // Arrange
        const string json = """
            { "mark": "point", "encoding": {
              "color": { "field": "a", "type": "quantitative", "legend": null },
              "fill": { "field": "b", "type": "quantitative", "scale": { "scheme": { "name": "viridis", "count": 5, "extent": [0.1, 0.9] }, "domainMid": 0, "domainMin": -1, "domainMax": 1, "interpolate": { "type": "hcl", "gamma": 2 }, "type": "sequential" }, "legend": null },
              "stroke": { "field": "c", "type": "nominal", "scale": { "scheme": "category20" }, "legend": null },
              "opacity": { "field": "d", "type": "quantitative", "scale": { "scheme": "blues", "type": "quantize", "rangeMin": 0.1, "rangeMax": 1 }, "legend": null }
            } }
            """;

        // Act
        var ids = Visit(json);

        // Assert
        Assert.Empty(ids);
    }

    /// <summary>
    ///     Validates the cubehelix interpolation report: both spellings, as a string and as an object, and no report for the other types.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_CubehelixInterpolationIsReported()
    {
        // Arrange
        const string json = """
            { "mark": "point", "encoding": {
              "color": { "field": "a", "type": "quantitative", "scale": { "interpolate": "cubehelix" }, "legend": null },
              "fill": { "field": "b", "type": "quantitative", "scale": { "interpolate": { "type": "cubehelix-long", "gamma": 1 } }, "legend": null },
              "stroke": { "field": "c", "type": "quantitative", "scale": { "interpolate": "lab" }, "legend": null }
            } }
            """;

        // Act
        var ids = Visit(json);

        // Assert
        Assert.Equal(
            [
                ("vl-scale-interpolate-cubehelix", "/encoding/color/scale/interpolate"),
                ("vl-scale-interpolate-cubehelix", "/encoding/fill/scale/interpolate"),
            ],
            ids);
    }

    /// <summary>
    ///     Validates that a shape that is not a symbol name is reported wherever it can be given (the mark, the channel value, the scale
    ///     range), and that every symbol name the painter draws is not.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_CustomPathShapesAreReported()
    {
        // Arrange
        const string json = """
            { "mark": { "type": "point", "shape": "M0,0L1,1" }, "encoding": {
              "shape": { "field": "a", "type": "nominal", "scale": { "range": ["circle", "M0,0L1,1Z", "wedge"] }, "legend": null }
            } }
            """;
        const string value = """
            { "mark": "point", "encoding": { "shape": { "value": "M0,0L1,1" } } }
            """;
        var known = string.Join(
            ",",
            new[] { "circle", "square", "cross", "diamond", "arrow", "wedge", "triangle", "triangle-up", "triangle-down", "triangle-left", "triangle-right", "stroke" }.Select(s => $"\"{s}\""));

        // Act
        var ids = Visit(json);
        var channelValue = Visit(value);
        var symbols = Visit("{\"mark\":{\"type\":\"point\",\"shape\":\"cross\"},\"encoding\":{\"shape\":{\"field\":\"a\",\"type\":\"nominal\",\"scale\":{\"range\":[" + known + "]},\"legend\":null}}}");

        // Assert
        Assert.Equal(
            [
                ("vl-shape-path", "/mark/shape"),
                ("vl-shape-path", "/encoding/shape/scale/range/1"),
            ],
            ids);
        Assert.Equal([("vl-shape-path", "/encoding/shape/value")], channelValue);
        Assert.Empty(symbols);
    }

    /// <summary>
    ///     Validates that the shape, angle and stroke dash channels are reported (and ignored) on a mark that does not draw them, and are not
    ///     reported on one that does.
    /// </summary>
    /// <param name="mark">The mark type.</param>
    /// <param name="channel">The channel with its definition.</param>
    /// <param name="drawn">Whether the mark draws the channel.</param>
    [Theory]
    [InlineData("point", "\"shape\":{\"value\":\"cross\"}", true)]
    [InlineData("circle", "\"shape\":{\"value\":\"cross\"}", false)]
    [InlineData("bar", "\"shape\":{\"value\":\"cross\"}", false)]
    [InlineData("point", "\"angle\":{\"value\":10}", true)]
    [InlineData("square", "\"angle\":{\"value\":10}", true)]
    [InlineData("tick", "\"angle\":{\"value\":10}", false)]
    [InlineData("bar", "\"strokeDash\":{\"value\":[2,2]}", true)]
    [InlineData("rule", "\"strokeDash\":{\"value\":[2,2]}", true)]
    [InlineData("rect", "\"strokeDash\":{\"value\":[2,2]}", false)]
    public void FeatureCatalog_Visit_ShapeAngleAndDashChannelsAreReportedForMarksThatDoNotDrawThem(string mark, string channel, bool drawn)
    {
        // Act
        var ids = Visit("{\"mark\":\"" + mark + "\",\"encoding\":{" + channel + "}}");

        // Assert
        var name = channel[1..channel.IndexOf('"', 1)];
        Assert.Equal(drawn ? [] : [("vl-channel-" + name.ToLowerInvariant(), "/encoding/" + name)], ids);
    }

    /// <summary>
    ///     Validates the scale type warning: a type the channel or the field type cannot use, a quantitative field on a shape or stroke dash
    ///     channel, and no warning for accepted types, for types that are reported otherwise, or for a channel without a field.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_ScaleTypeIgnoredWarnsAboutTypesThatCannotBeUsed()
    {
        // Arrange
        const string json = """
            { "mark": "point", "encoding": {
              "x": { "field": "a", "type": "quantitative", "scale": { "type": "band" } },
              "y": { "field": "b", "type": "nominal", "scale": { "type": "linear" } },
              "color": { "field": "c", "type": "nominal", "scale": { "type": "sequential" }, "legend": null },
              "fill": { "field": "d", "type": "quantitative", "scale": { "type": "quantize" }, "legend": null },
              "stroke": { "field": "e", "type": "quantitative", "scale": { "type": "log" }, "legend": null },
              "size": { "field": "f", "type": "quantitative", "scale": { "type": "threshold", "domain": [1] }, "legend": null },
              "shape": { "field": "g", "type": "quantitative", "legend": null },
              "strokeDash": { "field": "h", "type": "quantitative", "scale": { "type": "quantile" }, "legend": null },
              "opacity": { "value": 1, "scale": { "type": "band" } }
            } }
            """;

        // Act
        var reports = FeatureCatalog.Visit(SpecParser.Parse(json));

        // Assert: log is reported as an unsupported type only; band on a quantitative field, linear on a nominal one, sequential on a nominal
        // color and the quantitative shape each warn
        var warnings = reports.Where(r => r.Id == "vl-scale-type-ignored").Select(r => (r.Kind, r.Location.Pointer)).ToList();
        Assert.Equal(
            [
                (FeatureReportKind.Warning, "/encoding/x/scale/type"),
                (FeatureReportKind.Warning, "/encoding/y/scale/type"),
                (FeatureReportKind.Warning, "/encoding/color/scale/type"),
                (FeatureReportKind.Warning, "/encoding/shape/type"),
            ],
            warnings);
        Assert.Contains(reports, r => r.Id == "vl-scale-log");
        Assert.DoesNotContain(reports, r => r.Id == "vl-scale-quantize");
    }

    /// <summary>
    ///     Validates that a sort that orders categories is not ignored on a discretizing color scale, and that scheme names are never
    ///     catalog reports (an unknown name is a specification error of the scale resolver).
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_SchemeNamesAreNotReported()
    {
        // Arrange
        const string json = """
            { "mark": "point", "encoding": {
              "color": { "field": "a", "type": "quantitative", "scale": { "type": "quantize", "scheme": "Blues" }, "sort": ["a"], "legend": null },
              "fill": { "field": "b", "type": "nominal", "scale": { "scheme": "TABLEAU10" }, "legend": null },
              "stroke": { "field": "c", "type": "nominal", "scale": { "scheme": "no-such-scheme" }, "legend": null }
            } }
            """;

        // Act
        var ids = Visit(json);

        // Assert
        Assert.Empty(ids);
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
    ///     Validates that no bar chart is reported for being stacked implicitly (the stack is computed now), whatever the channels.
    /// </summary>
    /// <param name="json">The specification text.</param>
    [Theory]
    [InlineData("{\"mark\":\"bar\",\"encoding\":{\"x\":{\"field\":\"k\",\"type\":\"nominal\"},\"y\":{\"field\":\"v\",\"type\":\"quantitative\"},\"color\":{\"field\":\"g\",\"type\":\"nominal\",\"legend\":null}}}")]
    [InlineData("{\"mark\":{\"type\":\"bar\"},\"encoding\":{\"y\":{\"field\":\"k\",\"type\":\"ordinal\"},\"x\":{\"field\":\"v\",\"type\":\"quantitative\"},\"detail\":[{\"field\":\"g\"}]}}")]
    [InlineData("{\"mark\":\"bar\",\"encoding\":{\"y\":{\"field\":\"v\",\"type\":\"quantitative\"},\"size\":{\"field\":\"g\",\"type\":\"nominal\",\"legend\":null}}}")]
    [InlineData("{\"mark\":\"bar\",\"encoding\":{\"x\":{\"field\":\"k\",\"type\":\"nominal\"},\"y\":{\"field\":\"v\",\"type\":\"quantitative\",\"stack\":null},\"color\":{\"field\":\"g\",\"type\":\"nominal\",\"legend\":null}}}")]
    [InlineData("{\"mark\":\"bar\",\"encoding\":{\"x\":{\"field\":\"k\",\"type\":\"nominal\"},\"y\":{\"field\":\"v\",\"type\":\"quantitative\"},\"y2\":{\"field\":\"w\"},\"color\":{\"field\":\"g\",\"type\":\"nominal\",\"legend\":null}}}")]
    public void FeatureCatalog_Visit_ImplicitBarStackIsNotReported(string json)
    {
        // Arrange / Act
        var ids = Visit(json).Select(r => r.Id).ToList();

        // Assert: no report is about stacking (the detail channel of the second chart is reported for what it is)
        Assert.DoesNotContain("vl-encoding-stack", ids);
        Assert.DoesNotContain(ids, id => id.StartsWith("vl-encoding-", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that the <c>order</c> channel is accepted when it orders a stack (also for a bar with two measures, whose orientation Vega-Lite infers as vertical,
    ///     and for one whose mark gives the orientation) and reported otherwise.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="reported">Whether <c>vl-channel-order</c> is expected.</param>
    [Theory]
    [InlineData("{\"mark\":\"bar\",\"encoding\":{\"x\":{\"field\":\"k\",\"type\":\"nominal\"},\"y\":{\"field\":\"v\",\"type\":\"quantitative\"},\"color\":{\"field\":\"g\",\"type\":\"nominal\",\"legend\":null},\"order\":{\"field\":\"o\"}}}", false)]
    [InlineData("{\"mark\":\"bar\",\"encoding\":{\"x\":{\"field\":\"k\",\"type\":\"nominal\"},\"y\":{\"field\":\"v\",\"type\":\"quantitative\"},\"color\":{\"field\":\"g\",\"type\":\"nominal\",\"legend\":null},\"order\":[{\"field\":\"o\"},{\"field\":\"p\",\"sort\":\"descending\"}]}}", false)]
    [InlineData("{\"mark\":\"bar\",\"encoding\":{\"x\":{\"field\":\"k\",\"type\":\"nominal\"},\"y\":{\"field\":\"v\",\"type\":\"quantitative\",\"stack\":null},\"order\":{\"field\":\"o\"}}}", true)]
    [InlineData("{\"mark\":\"point\",\"encoding\":{\"x\":{\"field\":\"k\",\"type\":\"nominal\"},\"y\":{\"field\":\"v\",\"type\":\"quantitative\"},\"order\":{\"field\":\"o\"}}}", true)]
    [InlineData("{\"mark\":\"point\",\"encoding\":{\"x\":{\"field\":\"k\",\"type\":\"nominal\"},\"y\":{\"field\":\"v\",\"type\":\"quantitative\",\"stack\":true},\"color\":{\"field\":\"g\",\"type\":\"nominal\",\"legend\":null},\"order\":{\"field\":\"o\"}}}", false)]
    [InlineData("{\"mark\":\"bar\",\"encoding\":{\"x\":{\"field\":\"a\",\"type\":\"quantitative\"},\"y\":{\"field\":\"v\",\"type\":\"quantitative\"},\"color\":{\"field\":\"g\",\"type\":\"nominal\",\"legend\":null},\"order\":{\"field\":\"o\"}}}", false)]
    [InlineData("{\"mark\":{\"type\":\"bar\",\"orient\":\"horizontal\"},\"encoding\":{\"x\":{\"field\":\"a\",\"type\":\"quantitative\"},\"y\":{\"field\":\"v\",\"type\":\"quantitative\"},\"color\":{\"field\":\"g\",\"type\":\"nominal\",\"legend\":null},\"order\":{\"field\":\"o\"}}}", false)]
    public void FeatureCatalog_Visit_StackOrderChannelIsAcceptedOnlyForAStack(string json, bool reported)
    {
        // Arrange / Act
        var ids = Visit(json).Select(r => r.Id).ToList();

        // Assert
        Assert.Equal(reported ? new[] { "vl-channel-order" } : [], ids);
    }

    /// <summary>
    ///     Validates that a secondary position channel is accepted for a bar, rect or rule that has the channel it extends, and reported otherwise.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="expectedId">The identifier that is expected, or null when there must be no report.</param>
    [Theory]
    [InlineData("{\"mark\":\"bar\",\"encoding\":{\"x\":{\"field\":\"a\",\"type\":\"quantitative\"},\"x2\":{\"field\":\"b\"}}}", null)]
    [InlineData("{\"mark\":\"rect\",\"encoding\":{\"y\":{\"field\":\"a\",\"type\":\"quantitative\"},\"y2\":{\"field\":\"b\"}}}", null)]
    [InlineData("{\"mark\":\"rule\",\"encoding\":{\"x\":{\"field\":\"a\",\"type\":\"quantitative\"},\"x2\":{\"value\":3}}}", null)]
    [InlineData("{\"mark\":\"point\",\"encoding\":{\"x\":{\"field\":\"a\",\"type\":\"quantitative\"},\"x2\":{\"field\":\"b\"}}}", "vl-channel-x2")]
    [InlineData("{\"mark\":\"tick\",\"encoding\":{\"y\":{\"field\":\"a\",\"type\":\"quantitative\"},\"y2\":{\"field\":\"b\"}}}", "vl-channel-y2")]
    [InlineData("{\"mark\":\"bar\",\"encoding\":{\"y\":{\"field\":\"a\",\"type\":\"quantitative\"},\"x2\":{\"field\":\"b\"}}}", "vl-channel-x2")]
    public void FeatureCatalog_Visit_SecondaryPositionChannelsAreAcceptedForRanges(string json, string? expectedId)
    {
        // Arrange / Act
        var ids = Visit(json).Select(r => r.Id).ToList();

        // Assert
        Assert.Equal(expectedId is null ? [] : new[] { expectedId }, ids);
    }

    /// <summary>
    ///     Validates that an explicit stack on a scale that is not linear is reported, and a stack on a linear scale is not.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_StackOnANonLinearScaleIsReported()
    {
        // Arrange
        const string json = """
            { "mark": "bar", "encoding": {
              "x": { "field": "k", "type": "nominal" },
              "y": { "field": "v", "type": "quantitative", "stack": "center", "scale": { "type": "log" } }
            } }
            """;

        // Act
        var reports = Visit(json);

        // Assert
        Assert.Equal([("vl-encoding-stack-nonlinear", "/encoding/y/stack"), ("vl-scale-log", "/encoding/y/scale/type")], reports);
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

        // Assert: the filter object, the variable parameter (bind is ignored) and the row-bound color expression are supported
        Assert.Equal(
            [
                ("vl-expr-ref", "/width"),
                ("vl-param-selection", "/params/1"),
                ("vl-transform-timeunit", "/transform/1"),
                ("vl-expr-ref", "/mark/size"),
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

    /// <summary>
    ///     Validates that the Phase 5 transforms, the variable parameters and the row-bound expressions of the drawn view are
    ///     supported: a specification that uses all of them has no reports.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_SupportedPhase5TransformsAndParametersAreNotReported()
    {
        // Arrange
        const string json = """
            {
              "params": [ { "name": "k", "value": 2 }, { "name": "kk", "expr": "k * 2", "bind": { "input": "range" } } ],
              "data": { "values": [ { "a": 1, "b": [1, 2], "c": "x" } ] },
              "transform": [
                { "calculate": "datum.a * kk + abs(-1)", "as": "d" },
                { "filter": "datum.d > k && isValid(datum.a)" },
                { "filter": { "and": [ { "field": "a", "range": [0, 9] }, { "not": { "field": "a", "oneOf": [3, 4] } }, { "param": "k" } ] } },
                { "fold": ["a", "d"] },
                { "flatten": ["b"] },
                { "sample": 5 },
                { "pivot": "key", "value": "value", "groupby": ["c"], "op": "sum" },
                { "lookup": "c", "from": { "data": { "name": "other" }, "key": "k", "fields": ["v"] }, "default": 0 }
              ],
              "datasets": { "other": [ { "k": "x", "v": 1 } ] },
              "mark": { "type": "point", "color": { "expr": "datum.a > 1 ? 'red' : 'blue'" }, "size": { "expr": "30 + k" }, "opacity": { "expr": "0.5" }, "strokeWidth": { "expr": "k" } },
              "encoding": { "color": { "value": { "expr": "'green'" } }, "size": { "value": { "expr": "k * 10" } }, "opacity": { "value": { "expr": "0.7" } } }
            }
            """;

        // Act
        var ids = Visit(json);

        // Assert
        Assert.Empty(ids);
    }

    /// <summary>
    ///     Validates that the constructs of a supported expression that the engine does not execute are reported at the
    ///     expression text with stable identifiers: unlisted functions, view state, deferred operators and view signals.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_UnsupportedExpressionConstructsAreReportedAtTheExpression()
    {
        // Arrange
        const string json = """
            {
              "params": [ { "name": "p", "expr": "datetime(2020, 1, 1)" }, { "name": "width", "value": 3 } ],
              "data": { "values": [ { "a": 1 } ] },
              "transform": [
                { "calculate": "timeFormat(datum.d, '%Y')", "as": "y" },
                { "filter": "signal('a') || event.x" },
                { "calculate": "datum.a & 3 in datum.b", "as": "z" },
                { "calculate": "height / 2 + width", "as": "w" },
                { "filter": { "or": [ "rgb(1, 2, 3)", { "field": "a", "equal": 1 } ] } }
              ],
              "mark": "point",
              "encoding": { "color": { "value": { "expr": "format(datum.a, '.1f')" } } }
            }
            """;

        // Act
        var reports = FeatureCatalog.Visit(SpecParser.Parse(json));

        // Assert: one report per construct, at the expression text, in document order (width is a declared parameter here)
        Assert.Equal(
            [
                ("vl-expr-function-datetime", "/params/0/expr"),
                ("vl-expr-function-timeformat", "/transform/0/calculate"),
                ("vl-expr-view-state", "/transform/1/filter"),
                ("vl-expr-view-state", "/transform/1/filter"),
                ("vl-expr-construct-bitwise-and", "/transform/2/calculate"),
                ("vl-expr-construct-in", "/transform/2/calculate"),
                ("vl-expr-view-state", "/transform/3/calculate"),
                ("vl-expr-function-rgb", "/transform/4/filter/or/0"),
                ("vl-expr-function-format", "/encoding/color/value/expr"),
            ],
            reports.Select(r => (r.Id, r.Location.Pointer)));
        Assert.All(reports, r => Assert.Equal(FeatureReportKind.UnsupportedFeature, r.Kind));
        Assert.Contains("(at expression offset 0)", reports[0].Message, StringComparison.Ordinal);
        Assert.Contains("'height'", reports[6].Message, StringComparison.Ordinal);

        // Assert: the identifier is lower case while the message keeps the function name as written
        Assert.Contains("'timeFormat'", reports[1].Message, StringComparison.Ordinal);

        // Assert: the location is that of the string value, so the first report is at line 2 (the parameter) and a strict render stops there
        Assert.Equal(2, reports[0].Location.Line);
    }

    /// <summary>
    ///     Validates that differently-cased spellings of one unlisted function share the lower-case identifier, and that
    ///     every call is still reported once, in order of appearance, with the spelling as written in the message.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_DifferentlyCasedFunctionNames_ShareOneLowerCaseIdentifier()
    {
        // Arrange
        const string json = """
            {
              "data": { "values": [ { "a": 1 } ] },
              "transform": [ { "calculate": "timeFormat(datum.d, '%Y') + timeformat(datum.d, '%Y') + TIMEFORMAT(datum.d, '%Y')", "as": "y" } ],
              "mark": "point",
              "encoding": { "x": { "field": "a", "type": "quantitative" } }
            }
            """;

        // Act
        var reports = FeatureCatalog.Visit(SpecParser.Parse(json));

        // Assert: three reports with one identifier and distinct messages, in offset order
        Assert.Equal(3, reports.Count);
        Assert.All(reports, r => Assert.Equal("vl-expr-function-timeformat", r.Id));
        Assert.Contains("'timeFormat'", reports[0].Message, StringComparison.Ordinal);
        Assert.Contains("'timeformat'", reports[1].Message, StringComparison.Ordinal);
        Assert.Contains("'TIMEFORMAT'", reports[2].Message, StringComparison.Ordinal);
        Assert.Equal(3, reports.Select(r => r.Message).Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    ///     Validates that the transforms of a later phase, conditional encodings and selection parameters are still reported, and that
    ///     the aggregating transforms of the drawn view are accepted without a report.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_LaterPhaseTransformsConditionsAndSelectionsStayReported()
    {
        // Arrange
        const string json = """
            {
              "params": [ { "name": "s", "select": "interval" } ],
              "data": { "values": [ { "a": 1 } ] },
              "transform": [
                { "aggregate": [ { "op": "count", "as": "n" } ] },
                { "joinaggregate": [ { "op": "sum", "field": "v", "as": "t" } ] },
                { "window": [ { "op": "rank", "as": "r" } ] },
                { "bin": true, "field": "x", "as": "b" },
                { "stack": "v", "as": ["a", "b"] },
                { "impute": "v", "key": "k" },
                { "timeUnit": "year", "field": "d", "as": "y" },
                { "regression": "y", "on": "x" },
                { "loess": "y", "on": "x" },
                { "density": "x" },
                { "quantile": "x" },
                { "extent": "x", "param": "e" }
              ],
              "mark": "point",
              "encoding": { "color": { "condition": { "param": "s", "value": "red" }, "value": "gray" } }
            }
            """;

        // Act
        var ids = Visit(json);

        // Assert: the six aggregating transforms are computed, the later phases are reported
        Assert.Equal(
            [
                ("vl-param-selection", "/params/0"), ("vl-transform-timeunit", "/transform/6"), ("vl-transform-regression", "/transform/7"),
                ("vl-transform-loess", "/transform/8"), ("vl-transform-density", "/transform/9"), ("vl-transform-quantile", "/transform/10"),
                ("vl-transform-extent", "/transform/11"), ("vl-encoding-condition", "/encoding/color/condition"),
            ],
            ids);
    }

    /// <summary>
    ///     Validates that every aggregating transform of a nested view (which is not drawn) is reported, and that a bin extent that is a
    ///     parameter is reported for the drawn view only.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_NestedViewAggregatingTransformsAreStillReported()
    {
        // Arrange
        const string nested = """
            {
              "layer": [
                { "mark": "point", "transform": [
                  { "aggregate": [ { "op": "count", "as": "n" } ] },
                  { "joinaggregate": [ { "op": "sum", "field": "v", "as": "t" } ] },
                  { "window": [ { "op": "rank", "as": "r" } ] },
                  { "bin": { "extent": { "param": "e" } }, "field": "x", "as": "b" },
                  { "stack": "v" },
                  { "impute": "v", "key": "k" }
                ] }
              ]
            }
            """;
        const string drawn = """
            { "mark": "point", "transform": [
              { "bin": { "extent": { "param": "e" } }, "field": "x", "as": "b" },
              { "bin": { "extent": [ 0, 10 ] }, "field": "x", "as": "c" }
            ] }
            """;

        // Act
        var nestedIds = Visit(nested);
        var drawnIds = Visit(drawn);

        // Assert
        Assert.Equal(
            [
                ("vl-composition-layer", "/layer"),
                ("vl-transform-aggregate", "/layer/0/transform/0"),
                ("vl-transform-joinaggregate", "/layer/0/transform/1"),
                ("vl-transform-window", "/layer/0/transform/2"),
                ("vl-transform-bin", "/layer/0/transform/3"),
                ("vl-transform-stack", "/layer/0/transform/4"),
                ("vl-transform-impute", "/layer/0/transform/5"),
            ],
            nestedIds);
        Assert.Equal([("vl-transform-bin-extent-param", "/transform/0/bin/extent")], drawnIds);
    }

    /// <summary>
    ///     Validates the parts of the aggregation features that are reported, and that the supported forms are not: bin on a channel other
    ///     than x and y, a bin extent that is a parameter, the order channel without a stack, a secondary channel that a mark cannot draw, the
    ///     impute of a channel, and a stack on a scale that is not linear.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_Phase6FeaturesAreNotReportedAndRemainingPartsAre()
    {
        // Arrange
        const string json = """
            { "mark": "bar", "encoding": {
              "x": { "field": "k", "type": "quantitative", "bin": { "maxbins": 5, "extent": { "param": "p" } } },
              "y": { "aggregate": "sum", "field": "v", "type": "quantitative", "stack": "normalize", "scale": { "type": "linear" } },
              "color": { "field": "c", "type": "nominal", "bin": true, "legend": null },
              "size": { "field": "c", "type": "nominal", "legend": null, "sort": { "encoding": "y", "order": "descending" } },
              "x2": { "field": "e" },
              "order": { "field": "o", "sort": "descending" }
            } }
            """;

        // Act
        var ids = Visit(json);

        // Assert: x2 draws a range and the order channel orders the stack of y; only the binned scale parts are reported
        Assert.Equal(
            [
                ("vl-encoding-bin-extent-param", "/encoding/x/bin/extent"),
                ("vl-encoding-bin", "/encoding/color/bin"),
            ],
            ids);
    }

    /// <summary>
    ///     Validates the parts of the supported transforms that are not supported: a selection in a filter, a time unit or a
    ///     date object in a predicate, a pivot operation, a lookup in a parameter and a remote lookup source.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_UnsupportedPartsOfSupportedTransformsAreReported()
    {
        // Arrange
        const string json = """
            {
              "params": [ { "name": "brush", "select": "interval" } ],
              "data": { "values": [ { "a": 1 } ] },
              "transform": [
                { "filter": { "param": "brush" } },
                { "filter": { "selection": "brush" } },
                { "filter": { "field": "x", "equal": 1, "timeUnit": "year" } },
                { "filter": { "field": "x", "oneOf": [ { "year": 2020 } ] } },
                { "filter": { "field": "x", "equal": { "year": 2020 } } },
                { "pivot": "k", "value": "v", "op": "mean" },
                { "pivot": "k", "value": "v", "op": "count" },
                { "lookup": "k", "from": { "param": "brush" } },
                { "lookup": "k", "from": { "data": { "url": "https://example.com/a.csv" }, "key": "a", "fields": ["b"] } }
              ],
              "mark": "point"
            }
            """;

        // Act
        var reports = Visit(json);

        // Assert
        Assert.Equal(
            [
                ("vl-param-selection", "/params/0"),
                ("vl-transform-filter-param", "/transform/0/filter/param"),
                ("vl-transform-filter-param", "/transform/1/filter/selection"),
                ("vl-transform-filter-timeunit", "/transform/2/filter/timeUnit"),
                ("vl-transform-filter-datetime", "/transform/3/filter/oneOf/0"),
                ("vl-transform-filter-datetime", "/transform/4/filter/equal"),
                ("vl-transform-pivot-op", "/transform/5/op"),
                ("vl-transform-lookup-param", "/transform/7/from/param"),
                ("vl-data-remote-url", "/transform/8/from/data"),
            ],
            reports);
    }

    /// <summary>
    ///     Validates that the transforms and the variable parameters of a nested view are reported (the view is not drawn),
    ///     while the variable parameters of the root are not.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_NestedViewTransformsAndVariableParametersAreReported()
    {
        // Arrange
        const string json = """
            {
              "params": [ { "name": "r", "value": 2 } ],
              "layer": [
                { "params": [ { "name": "v", "value": 1 } ], "transform": [ { "calculate": "1", "as": "x" }, { "lookup": "k", "from": { "param": "r" } } ], "mark": "point" }
              ]
            }
            """;

        // Act
        var ids = Visit(json);

        // Assert
        Assert.Equal(
            [
                ("vl-composition-layer", "/layer"),
                ("vl-param-variable", "/layer/0/params/0"),
                ("vl-transform-calculate", "/layer/0/transform/0"),
                ("vl-transform-lookup", "/layer/0/transform/1"),
            ],
            ids);
    }

    /// <summary>
    ///     Validates that the row-bound sites of an expression reference are evaluated and the others are still reported as
    ///     <c>vl-expr-ref</c>, with the constructs of an evaluated site reported at its expression.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_ExpressionReferencesAreReportedOnlyAtDeferredSites()
    {
        // Arrange: a symbol size and a color value are row-bound; a bar's size, a position value and the title are not
        const string json = """
            {
              "title": { "text": { "expr": "'t'" } },
              "width": { "expr": "200" },
              "mark": { "type": "bar", "size": { "expr": "3" }, "fill": { "expr": "toString(datum.a)" } },
              "encoding": { "color": { "value": { "expr": "regexp('a')" } }, "x": { "value": { "expr": "10" } }, "size": { "value": { "expr": "1" } } }
            }
            """;

        // Act
        var ids = Visit(json);

        // Assert
        Assert.Equal(
            [
                ("vl-expr-ref", "/title/text"),
                ("vl-expr-ref", "/width"),
                ("vl-expr-ref", "/mark/size"),
                ("vl-expr-ref", "/encoding/x/value"),
                ("vl-expr-ref", "/encoding/size/value"),
            ],
            ids);
    }

    /// <summary>
    ///     Validates which <c>sort</c> forms of a channel are reported: only the forms the scale resolver cannot compute.
    /// </summary>
    /// <param name="sort">The text of the <c>sort</c> value.</param>
    /// <param name="reported">Whether <c>vl-encoding-sort</c> is expected.</param>
    [Theory]
    [InlineData("\"ascending\"", false)]
    [InlineData("[\"b\", \"a\"]", false)]
    [InlineData("{\"op\":\"sum\",\"field\":\"v\"}", false)]
    [InlineData("{\"op\":\"count\"}", false)]
    [InlineData("{\"op\":\"median\",\"field\":\"v\",\"order\":\"descending\"}", false)]
    [InlineData("{\"op\":\"sum\",\"field\":\"v\",\"order\":null}", false)]
    [InlineData("{\"op\":\"valid\",\"field\":\"v\"}", false)]
    [InlineData("{\"op\":\"q1\",\"field\":\"v\",\"order\":\"descending\"}", false)]
    [InlineData("{\"field\":\"v\"}", false)]
    [InlineData("{\"encoding\":\"y\",\"order\":\"descending\"}", false)]
    [InlineData("{\"encoding\":\"color\"}", true)]
    [InlineData("{\"op\":\"values\",\"field\":\"v\"}", true)]
    [InlineData("{\"op\":\"exponential\",\"field\":\"v\"}", true)]
    [InlineData("\"-y\"", false)]
    [InlineData("\"x\"", false)]
    [InlineData("\"-color\"", true)]
    public void FeatureCatalog_Visit_SortByFieldIsReportedOnlyForUnsupportedForms(string sort, bool reported)
    {
        // Arrange
        var json = "{\"mark\":\"bar\",\"encoding\":{\"x\":{\"field\":\"k\",\"type\":\"nominal\",\"sort\":" + sort + "},\"y\":{\"field\":\"v\",\"type\":\"quantitative\"}}}";

        // Act
        var ids = Visit(json).Select(r => r.Id).ToList();

        // Assert
        Assert.Equal(reported ? new[] { "vl-encoding-sort" } : [], ids);
    }

    /// <summary>
    ///     Validates that a sort that refers to a channel the encoding lacks is reported as <c>vl-encoding-sort</c> with a message that names the channel
    ///     (the specification is the problem, not the renderer), at the sort, and that an operation that cannot order categories keeps the general message.
    /// </summary>
    /// <param name="encoding">The text of the encoding (x always has the sort).</param>
    /// <param name="channel">The channel the message must name, or null for the operation message.</param>
    [Theory]
    [InlineData("""{"x":{"field":"k","type":"nominal","sort":{"encoding":"y"}}}""", "y")]
    [InlineData("""{"x":{"field":"k","type":"nominal","sort":"-y"},"y":{"value":3}}""", "y")]
    [InlineData("""{"x":{"field":"k","type":"nominal","sort":"color"},"y":{"field":"v","type":"quantitative"}}""", "color")]
    [InlineData("""{"x":{"field":"k","type":"nominal","sort":{"encoding":"size","order":"descending"}},"size":{"value":3}}""", "size")]
    [InlineData("""{"x":{"field":"k","type":"nominal","sort":{"op":"values","field":"v"}},"y":{"field":"v","type":"quantitative"}}""", null)]
    public void FeatureCatalog_Visit_SortOfAMissingChannelNamesTheChannel(string encoding, string? channel)
    {
        // Act
        var reports = FeatureCatalog.Visit(SpecParser.Parse("{\"mark\":\"bar\",\"encoding\":" + encoding + "}"));

        // Assert
        var report = Assert.Single(reports);
        Assert.Equal("vl-encoding-sort", report.Id);
        Assert.Equal(FeatureReportKind.UnsupportedFeature, report.Kind);
        Assert.Equal("/encoding/x/sort", report.Location.Pointer);
        if (channel is null)
        {
            Assert.Contains("operation", report.Message, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains("refers to the '" + channel + "' channel", report.Message, StringComparison.Ordinal);
            Assert.Contains("sorted ascending", report.Message, StringComparison.Ordinal);
        }
    }

    /// <summary>
    ///     Validates that a sort that lists or computes an order of categories is a <c>vl-encoding-sort-ignored</c> warning on a quantitative field with a
    ///     continuous scale, and is not reported for the plain directions, for null, for an order-only object, for a discrete field, or when the scale type is
    ///     a discrete one.
    /// </summary>
    /// <param name="definition">The definition of the <c>x</c> channel.</param>
    /// <param name="reported">Whether the warning is expected.</param>
    [Theory]
    [InlineData("""{"field":"v","type":"quantitative","sort":["a"]}""", true)]
    [InlineData("""{"field":"v","type":"quantitative","sort":[]}""", true)]
    [InlineData("""{"field":"v","type":"quantitative","sort":"-y"}""", true)]
    [InlineData("""{"field":"v","type":"quantitative","sort":{"op":"sum","field":"w"}}""", true)]
    [InlineData("""{"field":"v","type":"quantitative","sort":{"field":"w"}}""", true)]
    [InlineData("""{"field":"v","type":"quantitative","sort":"ascending"}""", false)]
    [InlineData("""{"field":"v","type":"quantitative","sort":"descending"}""", false)]
    [InlineData("""{"field":"v","type":"quantitative","sort":null}""", false)]
    [InlineData("""{"field":"v","type":"quantitative","sort":{"order":"descending"}}""", false)]
    [InlineData("""{"field":"v","type":"nominal","sort":["a"]}""", false)]
    [InlineData("""{"field":"v","type":"ordinal","sort":"-y"}""", false)]
    [InlineData("""{"field":"v","type":"quantitative","scale":{"type":"linear"},"sort":["a"]}""", true)]
    public void FeatureCatalog_Visit_SortOnAContinuousScaleIsIgnoredWithAWarning(string definition, bool reported)
    {
        // Act: the y channel exists, so a channel shorthand is applicable
        var reports = FeatureCatalog.Visit(SpecParser.Parse("{\"mark\":\"point\",\"encoding\":{\"x\":" + definition + ",\"y\":{\"field\":\"w\",\"type\":\"quantitative\"}}}"));

        // Assert
        if (reported)
        {
            var report = Assert.Single(reports);
            Assert.Equal(("vl-encoding-sort-ignored", FeatureReportKind.Warning, "/encoding/x/sort"), (report.Id, report.Kind, report.Location.Pointer));
        }
        else
        {
            Assert.Empty(reports);
        }
    }
}
