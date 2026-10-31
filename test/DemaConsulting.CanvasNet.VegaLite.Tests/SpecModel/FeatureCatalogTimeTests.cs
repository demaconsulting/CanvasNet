using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.SpecModel;

/// <summary>
///     Tests of the feature catalog for the time constructs of Phase 8: the constructs that are supported now report nothing (temporal fields, time units, time and
///     UTC scales, predicates with time units and date-time objects, the <c>timeUnit</c> transform), and the parts that are not applied report their identifier
///     at the exact value, in strict mode too.
/// </summary>
public class FeatureCatalogTimeTests
{
    /// <summary>Gets the cancellation token of the running test, so a canceled test run stops promptly.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Lists the reports of a specification as identifier and pointer pairs, in document order.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <returns>The pairs.</returns>
    private static (string Id, string Pointer)[] Visit(string json) =>
        [.. FeatureCatalog.Visit(SpecParser.Parse(json)).Select(r => (r.Id, r.Location.Pointer))];

    /// <summary>
    ///     Lists the reports of a view with one encoding.
    /// </summary>
    /// <param name="mark">The JSON of the mark.</param>
    /// <param name="encoding">The JSON of the encoding.</param>
    /// <param name="extra">Extra top-level members, each followed by a comma, or empty.</param>
    /// <returns>The pairs.</returns>
    private static (string Id, string Pointer)[] VisitView(string mark, string encoding, string extra = "") =>
        Visit("{" + extra + "\"data\":{\"values\":[{\"d\":1}]},\"mark\":" + mark + ",\"encoding\":" + encoding + "}");

    /// <summary>
    ///     Validates that temporal fields, time units, time scales and the constructs around them report nothing.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_TemporalFieldsAndTimeUnitsAreSupported()
    {
        // Act
        var reports = Visit(
            """
            { "data": { "values": [ { "d": "2024-03-05", "v": 1 } ] },
              "transform": [ { "timeUnit": "yearmonth", "field": "d", "as": "m" }, { "timeUnit": { "unit": "month", "step": 2, "utc": true }, "field": "d", "as": "n" },
                             { "filter": { "field": "d", "timeUnit": "year", "range": [ 2020, 2024 ] } }, { "filter": { "field": "d", "oneOf": [ { "year": 2024 } ] } } ],
              "mark": "bar",
              "encoding": {
                "x": { "field": "d", "type": "temporal", "timeUnit": "yearmonth", "scale": { "type": "utc", "nice": "month", "domain": [ { "year": 2024 }, { "year": 2025 } ] }, "axis": { "tickCount": { "interval": "month", "step": 2 }, "format": "%Y" } },
                "y": { "field": "v", "type": "quantitative" },
                "color": { "field": "d", "type": "temporal", "scale": { "type": "time" }, "legend": { "tickCount": "year" } } } }
            """);

        // Assert
        Assert.Empty(reports);
    }

    /// <summary>
    ///     Validates the report of a binned time unit, in an encoding, with the object flag, and in a transform: the unit is ignored, strict mode delivers it, and the
    ///     identifier is at the <c>timeUnit</c> value.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_BinnedTimeUnitIsReportedAtTheValue()
    {
        // Act
        var named = VisitView("\"bar\"", """{"x":{"field":"d","type":"temporal","timeUnit":"binnedyearmonth"}}""");
        var flagged = VisitView("\"bar\"", """{"x":{"field":"d","type":"temporal","timeUnit":{"unit":"yearmonth","binned":true}}}""");
        var unbinned = VisitView("\"bar\"", """{"x":{"field":"d","type":"temporal","timeUnit":{"unit":"yearmonth","binned":false}}}""");

        // Assert
        Assert.Equal([("vl-timeunit-binned", "/encoding/x/timeUnit")], named);
        Assert.Equal([("vl-timeunit-binned", "/encoding/x/timeUnit")], flagged);
        Assert.Empty(unbinned);
    }

    /// <summary>
    ///     Validates the <c>maxbins</c> form: supported in an encoding and in the transform (the extent of the data resolves it), reported in a predicate, which has no data
    ///     extent to bin.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_MaxBinsTimeUnitIsReportedInPredicatesOnly()
    {
        // Act
        var encoding = VisitView("\"bar\"", """{"x":{"field":"d","type":"temporal","timeUnit":{"maxbins":10}}}""");
        var transform = Visit("""{"data":{"values":[{"d":1}]},"transform":[{"timeUnit":{"maxbins":10},"field":"d","as":"b"}],"mark":"point"}""");
        var predicate = Visit("""{"data":{"values":[{"d":1}]},"transform":[{"filter":{"field":"d","timeUnit":{"maxbins":10},"equal":1}}],"mark":"point"}""");

        // Assert
        Assert.Empty(encoding);
        Assert.Empty(transform);
        Assert.Equal([("vl-timeunit-maxbins", "/transform/0/filter/timeUnit")], predicate);
    }

    /// <summary>
    ///     Validates the band positions that are not applied: a position other than 0.5 on the bar or rect of a time unit is <c>vl-encoding-bandposition-rect</c> at the
    ///     value (the definition's, else the mark's or the configuration's <c>timeUnitBandPosition</c>), 0.5 and a point or tick are silent, and a position on a field without a
    ///     time unit (or with a bin) stays <c>vl-encoding-bandposition</c>.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_BandPositionIdentifiers()
    {
        // Arrange
        const string Unit = "\"type\":\"temporal\",\"timeUnit\":\"yearmonth\"";

        // Act
        var rect = VisitView("\"bar\"", "{\"x\":{\"field\":\"d\"," + Unit + ",\"bandPosition\":0.25}}");
        var half = VisitView("\"bar\"", "{\"x\":{\"field\":\"d\"," + Unit + ",\"bandPosition\":0.5}}");
        var point = VisitView("\"point\"", "{\"x\":{\"field\":\"d\"," + Unit + ",\"bandPosition\":0.25}}");
        var marked = VisitView("{\"type\":\"rect\",\"timeUnitBandPosition\":0.25}", "{\"x\":{\"field\":\"d\"," + Unit + "}}");
        var configured = VisitView("\"bar\"", "{\"x\":{\"field\":\"d\"," + Unit + "}}", "\"config\":{\"bar\":{\"timeUnitBandPosition\":0.75}},");
        var general = VisitView("\"bar\"", "{\"x\":{\"field\":\"d\"," + Unit + "}}", "\"config\":{\"mark\":{\"timeUnitBandPosition\":0.1}},");
        var tick = VisitView("{\"type\":\"tick\",\"timeUnitBandPosition\":0.25}", "{\"x\":{\"field\":\"d\"," + Unit + "}}");
        var plain = VisitView("\"bar\"", """{"x":{"field":"d","type":"quantitative","bandPosition":0.25}}""");
        var binned = VisitView("\"bar\"", """{"x":{"field":"d","type":"quantitative","bin":true,"bandPosition":0.25}}""");

        // Assert
        Assert.Equal([("vl-encoding-bandposition-rect", "/encoding/x/bandPosition")], rect);
        Assert.Empty(half);
        Assert.Empty(point);
        Assert.Equal([("vl-encoding-bandposition-rect", "/mark/timeUnitBandPosition")], marked);
        Assert.Equal([("vl-encoding-bandposition-rect", "/config/bar/timeUnitBandPosition")], configured);
        Assert.Equal([("vl-encoding-bandposition-rect", "/config/mark/timeUnitBandPosition")], general);
        Assert.Empty(tick);
        Assert.Equal([("vl-encoding-bandposition", "/encoding/x/bandPosition")], plain);
        Assert.Equal([("vl-encoding-bandposition", "/encoding/x/bandPosition")], binned);
    }

    /// <summary>
    ///     Validates that a date-time object as the <c>datum</c> of a channel is reported at the object (the channel is ignored), and a datum that is not one is not.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_DatumDatetimeIsReported()
    {
        // Act
        var datetime = VisitView("\"rule\"", """{"x":{"datum":{"year":2024,"month":3},"type":"temporal"}}""");
        var number = VisitView("\"rule\"", """{"x":{"datum":3,"type":"quantitative"}}""");
        var repeat = VisitView("\"rule\"", """{"x":{"datum":{"repeat":"column"},"type":"quantitative"}}""");

        // Assert
        Assert.Equal([("vl-encoding-datum-datetime", "/encoding/x/datum")], datetime);
        Assert.Empty(number);
        Assert.Empty(repeat);
    }

    /// <summary>
    ///     Validates the warnings for an interval that a scale that is not a time scale ignores: <c>vl-scale-nice-interval</c> for <c>nice</c> and
    ///     <c>vl-guide-axis-tickcount-interval</c> for the <c>tickCount</c> of an axis and of a legend; a time scale (the default of a temporal field, or the explicit type) is silent.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_IntervalsOnScalesThatAreNotTimeScales()
    {
        // Act
        var quantitative = VisitView(
            "\"point\"",
            """{"x":{"field":"d","type":"quantitative","scale":{"nice":"month"},"axis":{"tickCount":{"interval":"day","step":2}}},"color":{"field":"d","type":"quantitative","legend":{"tickCount":"year"}}}""");
        var temporalLinear = VisitView("\"point\"", """{"x":{"field":"d","type":"temporal","scale":{"type":"linear","nice":"month"}}}""");
        var timeOnDates = VisitView("\"point\"", """{"x":{"field":"d","type":"temporal","scale":{"type":"time","nice":"month"},"axis":{"tickCount":"day"}}}""");
        var defaultTime = VisitView("\"point\"", """{"x":{"field":"d","type":"temporal","scale":{"nice":{"interval":"day"}},"axis":{"tickCount":"day"}}}""");
        var numeric = VisitView("\"point\"", """{"x":{"field":"d","type":"quantitative","scale":{"nice":5},"axis":{"tickCount":4}}}""");

        // Assert
        Assert.Equal(
            [
                ("vl-scale-nice-interval", "/encoding/x/scale/nice"),
                ("vl-guide-axis-tickcount-interval", "/encoding/x/axis/tickCount"),
                ("vl-guide-axis-tickcount-interval", "/encoding/color/legend/tickCount"),
            ],
            quantitative);
        Assert.Equal([("vl-scale-type-ignored", "/encoding/x/scale/type")], temporalLinear);
        Assert.Empty(timeOnDates);
        Assert.Empty(defaultTime);
        Assert.Empty(numeric);
    }

    /// <summary>
    ///     Validates the <c>scale.type</c> table of temporal fields: <c>time</c> and <c>utc</c> are accepted (silently) on a temporal field, every other type is replaced by
    ///     the default with <c>vl-scale-type-ignored</c>, and <c>time</c> and <c>utc</c> on a quantitative, nominal or ordinal field are ignored with the same warning.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_TimeAndUtcScaleTypesFollowVegaLite()
    {
        // Act
        var accepted = VisitView("\"point\"", """{"x":{"field":"d","type":"temporal","scale":{"type":"utc"}},"y":{"field":"d","type":"temporal","scale":{"type":"time"}},"color":{"field":"d","type":"temporal","scale":{"type":"time"}}}""");
        var temporalRefused = VisitView(
            "\"point\"",
            """{"x":{"field":"d","type":"temporal","scale":{"type":"band"}},"color":{"field":"d","type":"temporal","scale":{"type":"sequential"}},"size":{"field":"d","type":"temporal","scale":{"type":"quantile"}}}""");
        var shape = VisitView("\"point\"", """{"shape":{"field":"d","type":"temporal","scale":{"type":"time"}}}""");
        var others = VisitView(
            "\"point\"",
            """{"x":{"field":"d","type":"nominal","scale":{"type":"time"}},"y":{"field":"d","type":"ordinal","scale":{"type":"utc"}},"size":{"field":"d","type":"quantitative","scale":{"type":"time"}}}""");

        // Assert
        Assert.Empty(accepted);
        Assert.Equal(
            [("vl-scale-type-ignored", "/encoding/x/scale/type"), ("vl-scale-type-ignored", "/encoding/color/scale/type"), ("vl-scale-type-ignored", "/encoding/size/scale/type")],
            temporalRefused);
        Assert.Equal([("vl-scale-type-ignored", "/encoding/shape/scale/type")], shape);

        // Assert: a temporal field on a channel that shows categories gets an ordinal scale, with the same warning as a quantitative one
        Assert.Equal([("vl-scale-type-ignored", "/encoding/shape/type")], VisitView("\"point\"", """{"shape":{"field":"d","type":"temporal"}}"""));
        Assert.Equal(
            [("vl-scale-type-ignored", "/encoding/x/scale/type"), ("vl-scale-type-ignored", "/encoding/y/scale/type"), ("vl-scale-type-ignored", "/encoding/size/scale/type")],
            others);
    }

    /// <summary>
    ///     Validates the warning for <c>scale.zero</c> on a temporal field (Vega-Lite drops it for a time scale), and that other fields and other types are silent.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_ZeroOnATimeScaleIsAWarning()
    {
        // Act
        var temporal = VisitView("\"point\"", """{"x":{"field":"d","type":"temporal","scale":{"zero":true}},"y":{"field":"d","type":"temporal","scale":{"type":"utc","zero":false}}}""");
        var quantitative = VisitView("\"point\"", """{"x":{"field":"d","type":"quantitative","scale":{"zero":true}}}""");
        var ignoredType = VisitView("\"point\"", """{"x":{"field":"d","type":"temporal","scale":{"type":"linear","zero":true}}}""");

        // Assert
        Assert.Equal([("vl-scale-zero-ignored", "/encoding/x/scale/zero"), ("vl-scale-zero-ignored", "/encoding/y/scale/zero")], temporal);
        Assert.Empty(quantitative);
        Assert.Equal([("vl-scale-type-ignored", "/encoding/x/scale/type")], ignoredType);
    }

    /// <summary>
    ///     Validates the warning for the parts of a time unit that a predicate does not apply (<c>week</c> and <c>dayofyear</c>), in a filter, at the time unit, inside a
    ///     composition, and that the same unit in an encoding is silent.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_WeekAndDayOfYearPartsOfPredicatesAreWarnings()
    {
        // Act
        var reports = FeatureCatalog.Visit(SpecParser.Parse(
            """
            { "data": { "values": [ { "d": 1 } ] },
              "transform": [ { "filter": { "field": "d", "timeUnit": "yearweek", "equal": { "year": 2024 } } },
                             { "filter": { "and": [ { "field": "d", "timeUnit": "dayofyear", "equal": 3 } ] } },
                             { "filter": { "field": "d", "timeUnit": "yearmonth", "equal": { "year": 2024 } } } ],
              "mark": "bar",
              "encoding": { "x": { "field": "d", "type": "temporal", "timeUnit": "yearweek" } } }
            """));

        // Assert: both are warnings
        Assert.Equal(
            [("vl-predicate-timeunit-part", "/transform/0/filter/timeUnit"), ("vl-predicate-timeunit-part", "/transform/1/filter/and/0/timeUnit")],
            reports.Select(r => (r.Id, r.Location.Pointer)));
        Assert.All(reports, r => Assert.Equal(FeatureReportKind.Warning, r.Kind));
    }

    /// <summary>
    ///     Validates that the <c>timeUnit</c> transform of a nested view of a composition is reported by the generic transform identifier (the view is not drawn), and that
    ///     the removed identifiers are gone: no report in this specification has one of them.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_NestedTimeUnitTransformIsReportedAndRemovedIdsAreGone()
    {
        // Act
        var reports = Visit(
            """
            { "layer": [ { "transform": [ { "timeUnit": "month", "field": "d", "as": "m" } ], "mark": "point" } ] }
            """);
        var everything = Visit(
            """
            { "data": { "values": [ { "d": "2024-03-05" } ] },
              "transform": [ { "filter": { "field": "d", "timeUnit": "month", "equal": 3 } }, { "filter": { "field": "d", "equal": { "year": 2024 } } } ],
              "mark": "point",
              "encoding": { "x": { "field": "d", "type": "temporal", "timeUnit": "month" },
                            "color": { "condition": { "test": { "field": "d", "timeUnit": "month", "equal": 3 }, "value": "red" }, "value": "gray" } } }
            """);

        // Assert
        Assert.Contains(("vl-transform-timeunit", "/layer/0/transform/0"), reports);
        Assert.Empty(everything);
    }

    /// <summary>
    ///     Validates the delivery through the public API: in strict mode a binned time unit throws at the first unsupported feature with its identifier and location, a
    ///     warning identifier never throws (the warning is delivered with its text), and the other reports are delivered once.
    /// </summary>
    [Fact]
    public void FeatureCatalog_Visit_TimeIdsAreDeliveredAndStrictModeThrows()
    {
        // Arrange
        const string Binned = """{"data":{"values":[{"d":"2024-03-05","v":1}]},"mark":"point","encoding":{"x":{"field":"v","type":"quantitative","timeUnit":"binnedyearmonth"}}}""";
        const string Warned = """{"data":{"values":[{"d":"2024-03-05","v":1}]},"transform":[{"filter":{"field":"d","timeUnit":"yearweek","equal":{"year":2024}}}],"mark":"point","encoding":{"x":{"field":"v","type":"quantitative"}}}""";
        var features = new List<string>();
        var warnings = new List<string>();
        var lenient = new VegaLiteRenderOptions { UnsupportedFeatureHandler = u => features.Add(u.Feature + " " + u.Location.Pointer), WarningHandler = w => warnings.Add(w.Message) };

        // Act
        var strict = Assert.Throws<VegaLiteUnsupportedFeatureException>(() => VegaLiteDocument.Parse(Binned).Render(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true }, Ct));
        using var drawn = VegaLiteDocument.Parse(Binned).Render(lenient, Ct);
        using var warned = VegaLiteDocument.Parse(Warned).Render(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true, WarningHandler = w => warnings.Add(w.Message) }, Ct);

        // Assert
        Assert.Equal("vl-timeunit-binned", strict.Feature);
        Assert.Equal(["vl-timeunit-binned /encoding/x/timeUnit"], features);
        Assert.Contains(warnings, message => message.Contains("week", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates the narrowing of <c>vl-format-type-custom</c>: the format types that the renderer evaluates (<c>number</c>, <c>time</c> and <c>utc</c>) report nothing on
    ///     an axis or a legend, and any other name is reported at the <c>formatType</c> property.
    /// </summary>
    /// <param name="formatType">The format type.</param>
    /// <param name="reported">Whether the format type is custom, so that it is reported.</param>
    [Theory]
    [InlineData("number", false)]
    [InlineData("time", false)]
    [InlineData("utc", false)]
    [InlineData("custom", true)]
    [InlineData("Time", true)]
    public void FeatureCatalog_Visit_FormatTypesNumberTimeAndUtcAreEvaluated(string formatType, bool reported)
    {
        // Act
        var axis = VisitView("\"point\"", "{\"x\":{\"field\":\"d\",\"type\":\"temporal\",\"axis\":{\"formatType\":\"" + formatType + "\"}}}");
        var legend = VisitView("\"point\"", "{\"color\":{\"field\":\"d\",\"type\":\"temporal\",\"legend\":{\"formatType\":\"" + formatType + "\"}}}");

        // Assert
        Assert.Equal(reported ? [("vl-format-type-custom", "/encoding/x/axis/formatType")] : [], axis);
        Assert.Equal(reported ? [("vl-format-type-custom", "/encoding/color/legend/formatType")] : [], legend);
        Assert.Equal(!reported, FeatureCatalog.IsEvaluatedFormatType(formatType));
    }
}
