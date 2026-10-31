using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Tests.Data;
using DemaConsulting.CanvasNet.VegaLite.Tests.Expressions;
using DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Scales;

/// <summary>
///     Tests of the scales of temporal fields and of fields with a time unit: the kind (time, UTC, sequential, band), the domain from the data, the band end of a
///     time-unit bar, explicit domains of dates and date-time objects, <c>nice</c>, padding, sorting, colors, zones and limits, with the instants computed with
///     the base class library and the numbers worked out by hand.
/// </summary>
public class ScaleResolverTimeTests
{
    /// <summary>Three dates ten days apart in total, with a measure.</summary>
    private const string Dates = """[{"d":"2024-01-01","v":1},{"d":"2024-01-06","v":2},{"d":"2024-01-11","v":3}]""";

    /// <summary>Two instants with a time of day, for <c>nice</c>.</summary>
    private const string Instants = """[{"d":"2024-01-10T13:00:00Z","v":1},{"d":"2024-03-20T05:00:00Z","v":2}]""";

    /// <summary>One day in milliseconds.</summary>
    private const double Day = 86_400_000;

    /// <summary>The tolerance of a computed instant or position.</summary>
    private const double Tolerance = 0.01;

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Resolves the scales of a specification the way a render does: the data stage in the calendar of a zone, the implicit encoding transforms and the scale inference.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="zone">The zone of the chart, or null for UTC.</param>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <returns>The scales.</returns>
    private static ResolvedScales Resolve(string json, TimeZoneInfo? zone = null, VegaLiteLimits? limits = null)
    {
        var (table, context, _, tree, _) = TimeTestPipeline.Prepare(json, zone, limits, null, parseWithDefaults: true);
        var rows = RowTransforms.Apply(table, tree.Root["transform"], context);
        var config = ConfigResolver.Resolve(tree, null);
        var plan = EncodingPlan.Create(tree.Root, config);
        var data = EncodingTransforms.Apply(plan, rows, context);
        return ScaleResolver.Resolve(tree.Root, plan, data, config, tree.SourceName, limits, Ct);
    }

    /// <summary>
    ///     Builds a specification.
    /// </summary>
    /// <param name="mark">The mark.</param>
    /// <param name="encoding">The encoding object text.</param>
    /// <param name="rows">The JSON array of rows.</param>
    /// <param name="extra">More top-level properties with a trailing comma, or empty.</param>
    /// <returns>The specification text.</returns>
    private static string Spec(string mark, string encoding, string rows = Dates, string extra = "") =>
        "{" + extra + "\"data\":{\"values\":" + rows + "},\"mark\":\"" + mark + "\",\"encoding\":" + encoding + "}";

    /// <summary>
    ///     Gets the instant of a date in 2024 (UTC).
    /// </summary>
    /// <param name="month">The month from 1.</param>
    /// <param name="day">The day.</param>
    /// <returns>The milliseconds since the epoch.</returns>
    private static double D(int month, int day) => TimeTestSupport.Ms(2024, month, day);

    /// <summary>
    ///     Asserts that two numbers are equal up to floating point noise.
    /// </summary>
    /// <param name="expected">The expected number.</param>
    /// <param name="actual">The actual number.</param>
    private static void Near(double expected, double? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected, actual.Value, Tolerance);
    }

    /// <summary>
    ///     Validates the default of a temporal x: a time scale over the extent of the dates, with no zero, no <c>nice</c> and no padding, and the model it makes.
    /// </summary>
    [Fact]
    public void ScaleResolver_Temporal_XDefaultsToATimeScaleWithTheDataExtent()
    {
        // Act
        var scales = Resolve(Spec("point", """{"x":{"field":"d","type":"temporal"},"y":{"field":"v","type":"quantitative"}}"""));
        var x = scales["x"]!;
        var model = x.CreateModel(400);

        // Assert: the domain is the dates (zero would pull it to 1970), without nice or padding
        Assert.Equal(ScaleKind.Time, x.Kind);
        Assert.Equal("temporal", x.DataType);
        Assert.Equal(D(1, 1), x.DomainStart);
        Assert.Equal(D(1, 11), x.DomainEnd);
        Assert.Null(x.NiceCount);
        Assert.Null(x.NiceInterval);
        Assert.Equal(0, x.PaddingPixels);
        Assert.True(x.IsTemporalField);
        Assert.Null(x.TimeUnit);
        Assert.True(x.Calendar!.IsUtc);
        Assert.Equal("d", x.FieldPath);

        // Assert: the model is a time scale over 0 to 400: January 6 is in the middle; the y scale of the measure is still linear
        Assert.Equal(ScaleKind.Time, model.Kind);
        Assert.Same(x.Calendar, model.Calendar);
        Near(200, model.Map(DataValue.FromDate(D(1, 6))));
        Near(0, model.Map(DataValue.FromDate(D(1, 1))));
        Near(400, model.Map(DataValue.FromDate(D(1, 11))));
        Assert.Equal(ScaleKind.Linear, scales["y"]!.Kind);

        // Assert: a temporal y runs upward like a linear one (the origin of a view is at the top)
        var y = Resolve(Spec("point", """{"y":{"field":"d","type":"temporal"}}""", Dates))["y"]!.CreateModel(300);
        Near(300, y.Map(DataValue.FromDate(D(1, 1))));
        Near(0, y.Map(DataValue.FromDate(D(1, 11))));
    }

    /// <summary>
    ///     Validates that a field with no valid date still gets a time scale (the default domain 0 to 1, as a linear scale does).
    /// </summary>
    [Fact]
    public void ScaleResolver_Temporal_EmptyDataGivesADefaultDomain()
    {
        // Act
        var x = Resolve(Spec("point", """{"x":{"field":"d","type":"temporal"}}""", "[]"))["x"]!;
        var unparsed = Resolve(Spec("point", """{"x":{"field":"d","type":"temporal"}}""", """[{"d":"not a date"},{"d":null}]"""))["x"]!;

        // Assert
        Assert.Equal(ScaleKind.Time, x.Kind);
        Assert.Equal((0d, 1d), (x.DomainStart, x.DomainEnd));
        Assert.Equal((0d, 1d), (unparsed.DomainStart, unparsed.DomainEnd));
        Near(100, x.CreateModel(100).Map(DataValue.FromDate(1)));
    }

    /// <summary>
    ///     Validates the UTC scale: a UTC time unit gives it, in any zone, with the UTC calendar; a local unit in a zone keeps the calendar of the zone.
    /// </summary>
    [Fact]
    public void ScaleResolver_UtcTimeUnit_GivesAUtcScale()
    {
        // Arrange
        var zone = TimeTestSupport.Fixed(5, 30);

        // Act
        var utc = Resolve(Spec("point", """{"x":{"field":"d","type":"temporal","timeUnit":"utcyearmonthdate"}}"""), zone)["x"]!;
        var local = Resolve(Spec("point", """{"x":{"field":"d","type":"temporal","timeUnit":"yearmonthdate"}}"""), zone)["x"]!;

        // Assert
        Assert.Equal(ScaleKind.Utc, utc.Kind);
        Assert.True(utc.Calendar!.IsUtc);
        Assert.True(utc.TimeUnit!.Utc);
        Assert.Equal("utcyearmonthdate", utc.TimeUnit.Name);
        Assert.Equal(ScaleKind.Time, local.Kind);
        Assert.False(local.Calendar!.IsUtc);
        Assert.Equal("yearmonthdate", local.TimeUnit!.Name);
        Assert.Equal(ScaleKind.Utc, utc.CreateModel(100).Kind);
    }

    /// <summary>
    ///     Validates the explicit types: <c>time</c> and <c>utc</c> on a temporal field (also on size, opacity, stroke width and angle), as the sequential scale on a color,
    ///     and the types a temporal field refuses (the default applies).
    /// </summary>
    [Fact]
    public void ScaleResolver_ExplicitTypeTimeAndUtc()
    {
        // Act
        ScaleKind KindOf(string channel, string type, string scale, string mark = "point") =>
            Resolve(Spec(mark, "{\"" + channel + "\":{\"field\":\"d\",\"type\":\"" + type + "\"" + scale + "}}"))[channel]!.Kind;

        // Assert: time and utc on a temporal field
        Assert.Equal(ScaleKind.Time, KindOf("x", "temporal", ",\"scale\":{\"type\":\"time\"}"));
        Assert.Equal(ScaleKind.Utc, KindOf("x", "temporal", ",\"scale\":{\"type\":\"utc\"}"));
        Assert.Equal(ScaleKind.Utc, KindOf("size", "temporal", ",\"scale\":{\"type\":\"utc\"}"));

        // Assert: a time scale on a color is the sequential scale over the instants
        Assert.Equal(ScaleKind.Sequential, KindOf("color", "temporal", ",\"scale\":{\"type\":\"time\"}"));
        Assert.Equal(ScaleKind.Sequential, KindOf("fill", "temporal", ",\"scale\":{\"type\":\"utc\"}"));

        // Assert: Vega-Lite accepts nothing but time and utc for a temporal field: the others are replaced by the default
        Assert.Equal(ScaleKind.Time, KindOf("x", "temporal", ",\"scale\":{\"type\":\"linear\"}"));
        Assert.Equal(ScaleKind.Time, KindOf("x", "temporal", ",\"scale\":{\"type\":\"band\"}"));
        Assert.Equal(ScaleKind.Time, KindOf("x", "temporal", ",\"scale\":{\"type\":\"ordinal\"}"));
        Assert.Equal(ScaleKind.Sequential, KindOf("color", "temporal", ",\"scale\":{\"type\":\"sequential\"}"));

        // Assert: the channels of a time scale and the discrete ones
        foreach (var channel in new[] { "size", "opacity", "strokeWidth", "fillOpacity", "strokeOpacity", "angle" })
        {
            Assert.Equal(ScaleKind.Time, KindOf(channel, "temporal", string.Empty));
        }

        Assert.Equal(ScaleKind.Ordinal, KindOf("shape", "temporal", string.Empty));
        Assert.Equal(ScaleKind.Ordinal, KindOf("strokeDash", "temporal", ",\"scale\":{\"type\":\"time\"}"));

        // Assert: time and utc are for temporal fields only
        Assert.Equal(ScaleKind.Band, KindOf("x", "nominal", ",\"scale\":{\"type\":\"time\"}", "bar"));
        Assert.Equal(ScaleKind.Point, KindOf("x", "ordinal", ",\"scale\":{\"type\":\"utc\"}"));
        Assert.Equal(ScaleKind.Linear, Resolve(Spec("point", """{"x":{"field":"v","type":"quantitative","scale":{"type":"time"}}}"""))["x"]!.Kind);
        Assert.Equal(ScaleKind.Sequential, Resolve(Spec("point", """{"color":{"field":"v","type":"quantitative","scale":{"type":"utc"}}}"""))["color"]!.Kind);
    }

    /// <summary>
    ///     Validates that the scale of the explicit-type acceptance table is the one the feature catalog uses.
    /// </summary>
    [Fact]
    public void ScaleResolver_AcceptsScaleType_PinsTheTemporalTable()
    {
        // Act / Assert: temporal takes time and utc on the position and numeric channels, and on a color (where they are sequential)
        foreach (var channel in new[] { "x", "y", "size", "opacity", "strokeWidth", "angle", "color", "fill", "stroke" })
        {
            Assert.True(ScaleResolver.AcceptsScaleType(channel, "temporal", "time"), channel);
            Assert.True(ScaleResolver.AcceptsScaleType(channel, "temporal", "utc"), channel);
        }

        // Assert: but not on shape and strokeDash; no other type is accepted for a temporal field
        Assert.False(ScaleResolver.AcceptsScaleType("shape", "temporal", "time"));
        Assert.False(ScaleResolver.AcceptsScaleType("strokeDash", "temporal", "utc"));
        foreach (var type in new[] { "linear", "log", "sequential", "band", "point", "ordinal", "quantile", "quantize", "threshold" })
        {
            Assert.False(ScaleResolver.AcceptsScaleType("x", "temporal", type), type);
            Assert.False(ScaleResolver.AcceptsScaleType("color", "temporal", type), type);
        }

        // Assert: and time and utc only for temporal fields
        foreach (var fieldType in new[] { "quantitative", "nominal", "ordinal" })
        {
            Assert.False(ScaleResolver.AcceptsScaleType("x", fieldType, "time"), fieldType);
            Assert.False(ScaleResolver.AcceptsScaleType("color", fieldType, "utc"), fieldType);
        }
    }

    /// <summary>
    ///     Validates the band end of a time-unit bar: the domain of the x scale runs from the start of the first month to the end of the last (January 1 to April 1, 2024), the
    ///     bar has no continuous padding and no <c>nice</c>, and the scale of February 1 is 31 of 91 days along.
    /// </summary>
    [Fact]
    public void ScaleResolver_TimeUnitBarDomainIncludesTheEnd()
    {
        // Arrange: a date in each of January, February and March (a bar spans its unit by the default band position 0.5)
        const string Rows = """[{"d":"2024-01-15"},{"d":"2024-02-20"},{"d":"2024-03-05"},{"d":"2024-03-06"}]""";

        // Act
        var x = Resolve(Spec("bar", """{"x":{"field":"d","type":"temporal","timeUnit":"yearmonth"},"y":{"aggregate":"count"}}""", Rows))["x"]!;
        var model = x.CreateModel(300);

        // Assert: the start and the end of the units
        Assert.Equal(ScaleKind.Time, x.Kind);
        Assert.Equal("yearmonth_d", x.FieldPath);
        Assert.Equal("yearmonth_d_end", x.Field2Path);
        Assert.Equal(D(1, 1), x.DomainStart);
        Assert.Equal(D(4, 1), x.DomainEnd);
        Assert.Equal("yearmonth", x.TimeUnit!.Name);
        Assert.Equal(0, x.PaddingPixels);
        Assert.Null(x.NiceCount);

        // Assert: 31 of 91 days of 300 pixels
        Near(31d / 91 * 300, model.Map(DataValue.FromDate(D(2, 1))));
    }

    /// <summary>
    ///     Validates that a point (which sits at the start of its unit) has no band end, so the domain holds the starts only; and that a bar without a unit
    ///     has the continuous band padding that a bar with one has not.
    /// </summary>
    [Fact]
    public void ScaleResolver_TimeUnitPointDomainIsTheStartsOnly()
    {
        // Arrange
        const string Rows = """[{"d":"2024-01-15"},{"d":"2024-02-20"},{"d":"2024-03-05"}]""";

        // Act
        var point = Resolve(Spec("point", """{"x":{"field":"d","type":"temporal","timeUnit":"yearmonth"}}""", Rows))["x"]!;

        // Assert: January 1 to March 1 (no end)
        Assert.Null(point.Field2Path);
        Assert.Equal(D(1, 1), point.DomainStart);
        Assert.Equal(D(3, 1), point.DomainEnd);
    }

    /// <summary>
    ///     Validates Vega-Lite's padding rule: a bar on a time axis with no unit gets half of its continuous band size (5 pixels); with a time unit it spans the unit and gets none;
    ///     <c>padding</c> and <c>config.scale.continuousPadding</c> apply to both.
    /// </summary>
    [Fact]
    public void ScaleResolver_BarPaddingNotAppliedForTimeUnit()
    {
        // Act
        var plain = Resolve(Spec("bar", """{"x":{"field":"d","type":"temporal"},"y":{"field":"v","type":"quantitative"}}"""))["x"]!;
        var unit = Resolve(Spec("bar", """{"x":{"field":"d","type":"temporal","timeUnit":"yearmonthdate"},"y":{"field":"v","type":"quantitative"}}"""))["x"]!;
        var explicitPadding = Resolve(Spec("bar", """{"x":{"field":"d","type":"temporal","timeUnit":"yearmonthdate","scale":{"padding":3}},"y":{"field":"v","type":"quantitative"}}"""))["x"]!;
        var configured = Resolve(Spec("bar", """{"x":{"field":"d","type":"temporal","timeUnit":"yearmonthdate"},"y":{"field":"v","type":"quantitative"}}""", Dates, "\"config\":{\"scale\":{\"continuousPadding\":7}},"))["x"]!;

        // Assert
        Assert.Equal(5, plain.PaddingPixels);
        Assert.Equal(0, unit.PaddingPixels);
        Assert.Equal(3, explicitPadding.PaddingPixels);
        Assert.Equal(7, configured.PaddingPixels);

        // Assert: the padding widens the domain around its middle: 400 pixels with 10 on each side is 400 / 380 of the span
        var padded = Resolve(Spec("point", """{"x":{"field":"d","type":"temporal","scale":{"padding":10}}}"""))["x"]!.CreateModel(400);
        var factor = 400d / 380;
        Near(D(1, 6) - (5 * Day * factor), padded.DomainStart);
        Near(D(1, 6) + (5 * Day * factor), padded.DomainEnd);
    }

    /// <summary>
    ///     Validates explicit domains of a temporal field: timestamps, date texts (a date alone is UTC, a time of day without an offset is local) and date-time objects (local unless
    ///     they say <c>utc</c>), in UTC and in a zone, and the errors for an entry that is not a date.
    /// </summary>
    [Fact]
    public void ScaleResolver_DomainStringsAndDatetimeObjects()
    {
        // Arrange
        var zone = TimeTestSupport.Fixed(5, 30);

        // Act
        ResolvedScale X(string domain, TimeZoneInfo? z = null) =>
            Resolve(Spec("point", "{\"x\":{\"field\":\"d\",\"type\":\"temporal\",\"scale\":{\"domain\":" + domain + "}}}"), z)["x"]!;

        // Assert: a date text and an object in UTC
        var mixed = X("""["2024-01-01", {"year":2024,"month":2,"date":1}]""");
        Assert.Equal((D(1, 1), D(2, 1)), (mixed.DomainStart, mixed.DomainEnd));
        Assert.Null(mixed.Domain);

        // Assert: a timestamp and an ISO date-time with an offset
        var stamps = X("""[1704067200000, "2024-03-01T05:30:00+05:30"]""");
        Assert.Equal((D(1, 1), D(3, 1)), (stamps.DomainStart, stamps.DomainEnd));

        // Assert: in a +05:30 zone the object is local midnight (the day before at 18:30Z), a date alone and a utc object are UTC, a time of day without an offset is local
        Assert.Equal(TimeTestSupport.Ms(2024, 1, 31, 18, 30), X("""["2024-01-01", {"year":2024,"month":2,"date":1}]""", zone).DomainEnd);
        Assert.Equal(D(1, 1), X("""["2024-01-01", {"year":2024,"month":2,"date":1}]""", zone).DomainStart);
        Assert.Equal(D(2, 1), X("""["2024-01-01", {"year":2024,"month":2,"date":1,"utc":true}]""", zone).DomainEnd);
        Assert.Equal(TimeTestSupport.Ms(2024, 1, 1, 6, 30), X("""["2024-01-01T12:00:00", "2024-02-01"]""", zone).DomainStart);

        // Assert: three entries make a piecewise scale; the entries keep their order
        var piecewise = X("""["2024-01-01", "2024-01-02", "2024-02-01"]""");
        Assert.Equal([D(1, 1), D(1, 2), D(2, 1)], piecewise.Domain);

        // Assert: an entry that is not a date, or too few entries, name the offending value
        var bad = Assert.Throws<VegaLiteSpecException>(() => X("""["2024-01-01", "garbage"]"""));
        Assert.Equal("/encoding/x/scale/domain/1", bad.Location.Pointer);
        var notADate = Assert.Throws<VegaLiteSpecException>(() => X("""[true, "2024-01-01"]"""));
        Assert.Equal("/encoding/x/scale/domain/0", notADate.Location.Pointer);
        var tooShort = Assert.Throws<VegaLiteSpecException>(() => X("""["2024-01-01"]"""));
        Assert.Equal("/encoding/x/scale/domain", tooShort.Location.Pointer);
    }

    /// <summary>
    ///     Validates the unit rule of Vega-Lite's value expression: for a field with a single local unit a number below 10000 or a text that is not a date names a part of
    ///     the unit (the month 3 is March 2012), and the unit's reference year is 2012.
    /// </summary>
    [Fact]
    public void ScaleResolver_DomainOfASingleUnit_NamesParts()
    {
        // Act
        var months = Resolve(Spec("point", """{"x":{"field":"d","type":"temporal","timeUnit":"month","scale":{"domain":[2, "Mar"]}}}"""))["x"]!;
        var instants = Resolve(Spec("point", """{"x":{"field":"d","type":"temporal","timeUnit":"month","scale":{"domain":[1325376000000, "2012-06-01"]}}}"""))["x"]!;

        // Assert: February 1 and March 1, 2012; and an instant and a date, which are what they say
        Assert.Equal((TimeTestSupport.Ms(2012, 2, 1), TimeTestSupport.Ms(2012, 3, 1)), (months.DomainStart, months.DomainEnd));
        Assert.Equal((1_325_376_000_000d, TimeTestSupport.Ms(2012, 6, 1)), (instants.DomainStart, instants.DomainEnd));
    }

    /// <summary>
    ///     Validates <c>domainMin</c> and <c>domainMax</c> of a temporal field: a date-time object and a timestamp replace the ends of the data domain.
    /// </summary>
    [Fact]
    public void ScaleResolver_DomainMinMaxDatetime()
    {
        // Act
        var bounded = Resolve(Spec("point", """{"x":{"field":"d","type":"temporal","scale":{"domainMin":{"year":2024,"month":1,"date":3},"domainMax":1706745600000}}}"""))["x"]!;
        var onlyMax = Resolve(Spec("point", """{"x":{"field":"d","type":"temporal","scale":{"domainMax":{"year":2024,"month":3}}}}"""))["x"]!;

        // Assert: January 3 to February 1; the data start with a new end (March 1, a month and a year of the object, the day being 1)
        Assert.Equal((D(1, 3), D(2, 1)), (bounded.DomainStart, bounded.DomainEnd));
        Assert.Equal((D(1, 1), D(3, 1)), (onlyMax.DomainStart, onlyMax.DomainEnd));
    }

    /// <summary>
    ///     Validates <c>nice</c> of a time scale: off by default, <c>true</c> is ten ticks (the week for 70 days), a number is the count, a text or <c>{interval, step}</c>
    ///     is the interval of the boundaries, and <c>false</c> and bad values switch it off.
    /// </summary>
    [Fact]
    public void ScaleResolver_NiceTrueAndInterval()
    {
        // Act
        ResolvedScale X(string scale) => Resolve(Spec("point", "{\"x\":{\"field\":\"d\",\"type\":\"temporal\"" + scale + "}}", Instants))["x"]!;

        var plain = X(string.Empty);
        var nice = X(",\"scale\":{\"nice\":true}");
        var count = X(",\"scale\":{\"nice\":2}");
        var month = X(",\"scale\":{\"nice\":\"month\"}");
        var quarter = X(",\"scale\":{\"nice\":{\"interval\":\"month\",\"step\":3}}");

        // Assert: no nice by default
        Assert.Null(plain.NiceCount);
        Assert.Equal(D(1, 10) + (13 * 3_600_000d), plain.CreateModel(400).DomainStart);

        // Assert: true is the count 10: 69.7 days / 10 is nearer to the week than to two days, so the Sundays January 7 and March 24
        Assert.Equal(10, nice.NiceCount);
        Assert.Equal((D(1, 7), D(3, 24)), (nice.CreateModel(400).DomainStart, nice.CreateModel(400).DomainEnd));

        // Assert: the count 2 gives a target of 34.8 days: the month
        Assert.Equal(2, count.NiceCount);
        Assert.Equal((D(1, 1), D(4, 1)), (count.CreateModel(400).DomainStart, count.CreateModel(400).DomainEnd));

        // Assert: an interval name: the boundaries of the month, and the quarters (January 1 to April 1)
        Assert.Null(month.NiceCount);
        Assert.Equal(TimeIntervalKind.Month, month.NiceInterval!.Value.Interval);
        Assert.Equal((D(1, 1), D(4, 1)), (month.CreateModel(400).DomainStart, month.CreateModel(400).DomainEnd));
        Assert.Equal(3, quarter.NiceInterval!.Value.Step);
        Assert.Equal((D(1, 1), D(4, 1)), (quarter.CreateModel(400).DomainStart, quarter.CreateModel(400).DomainEnd));

        // Assert: off
        foreach (var off in new[] { "false", "0", "-3" })
        {
            var scale = X(",\"scale\":{\"nice\":" + off + "}");
            Assert.Null(scale.NiceCount);
            Assert.Null(scale.NiceInterval);
        }
    }

    /// <summary>
    ///     Validates that <c>nice</c> follows the zone: the boundaries are local midnights.
    /// </summary>
    [Fact]
    public void ScaleResolver_NiceInAZone_UsesLocalBoundaries()
    {
        // Act: in +05:30 the month of January 10 13:00Z (18:30 local) starts at January 1 00:00 local, which is 18:30Z the day before
        var scale = Resolve(Spec("point", """{"x":{"field":"d","type":"temporal","scale":{"nice":"month"}}}""", Instants), TimeTestSupport.Fixed(5, 30))["x"]!.CreateModel(400);

        // Assert: December 31 18:30Z to March 31 18:30Z (April 1 00:00 local)
        Assert.Equal((TimeTestSupport.Ms(2023, 12, 31, 18, 30), TimeTestSupport.Ms(2024, 3, 31, 18, 30)), (scale.DomainStart, scale.DomainEnd));
    }

    /// <summary>
    ///     Validates the instants of a local time unit in a zone: the start of the day in +05:30 of an instant at 20:00Z, and that the domain follows it.
    /// </summary>
    [Fact]
    public void ScaleResolver_TimeUnit_InAZone_FollowsTheLocalDay()
    {
        // Arrange: 2024-03-05T20:00Z is 01:30 on March 6 in +05:30, so the day starts at March 5 18:30Z
        const string Rows = """[{"d":"2024-03-05T20:00:00Z"}]""";
        var zone = TimeTestSupport.Fixed(5, 30);

        // Act
        var local = Resolve(Spec("point", """{"x":{"field":"d","type":"temporal","timeUnit":"yearmonthdate"}}""", Rows), zone)["x"]!;
        var utc = Resolve(Spec("point", """{"x":{"field":"d","type":"temporal","timeUnit":"utcyearmonthdate"}}""", Rows), zone)["x"]!;

        // Assert
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 5, 18, 30), local.DomainStart);
        Assert.Equal(D(3, 5), utc.DomainStart);
    }

    /// <summary>
    ///     Validates an ordinal field with a time unit: a band scale (a bar) over the dates of the unit, sorted by date whatever the order of the data.
    /// </summary>
    [Fact]
    public void ScaleResolver_OrdinalTimeUnit_UsesABandScaleSortedByDate()
    {
        // Arrange: March, January, February and a second January
        const string Rows = """[{"d":"2024-03-10"},{"d":"2024-01-20"},{"d":"2024-02-05"},{"d":"2025-01-02"}]""";

        // Act
        var x = Resolve(Spec("bar", """{"x":{"field":"d","type":"ordinal","timeUnit":"month"},"y":{"aggregate":"count"}}""", Rows))["x"]!;

        // Assert: the unit is the month of 2012 (the reference year); the two Januaries are one category
        Assert.Equal(ScaleKind.Band, x.Kind);
        Assert.Equal("month_d", x.FieldPath);
        Assert.Equal("ordinal", x.DataType);
        Assert.True(x.IsTemporalField);
        Assert.Equal("month", x.TimeUnit!.Name);
        Assert.Equal(
            [DataValue.FromDate(TimeTestSupport.Ms(2012, 1, 1)), DataValue.FromDate(TimeTestSupport.Ms(2012, 2, 1)), DataValue.FromDate(TimeTestSupport.Ms(2012, 3, 1))],
            x.DiscreteDomain);
        Assert.Equal(x.DiscreteDomain.Count, x.CreateModel(300).Domain.Count);

        // Assert: a point scale for a point mark, a nominal field is the same
        var point = Resolve(Spec("point", """{"x":{"field":"d","type":"nominal","timeUnit":"month"}}""", Rows))["x"]!;
        Assert.Equal(ScaleKind.Point, point.Kind);
        Assert.Equal(3, point.DiscreteDomain.Count);
    }

    /// <summary>
    ///     Validates <c>sort: "descending"</c> of an ordinal time-unit field: the latest unit first.
    /// </summary>
    [Fact]
    public void ScaleResolver_OrdinalTimeUnit_SortDescending()
    {
        // Arrange
        const string Rows = """[{"d":"2024-03-10"},{"d":"2024-01-20"},{"d":"2024-02-05"}]""";

        // Act
        var x = Resolve(Spec("bar", """{"x":{"field":"d","type":"ordinal","timeUnit":"month","sort":"descending"},"y":{"aggregate":"count"}}""", Rows))["x"]!;

        // Assert
        Assert.Equal(
            [DataValue.FromDate(TimeTestSupport.Ms(2012, 3, 1)), DataValue.FromDate(TimeTestSupport.Ms(2012, 2, 1)), DataValue.FromDate(TimeTestSupport.Ms(2012, 1, 1))],
            x.DiscreteDomain);
    }

    /// <summary>
    ///     Validates a sort array and an explicit domain of a field with a month unit: the listed months come in that order (December, then January) after the ones it does
    ///     not list (February), and the month names, numbers and date-time objects of a domain are converted to the same dates as the unit's starts.
    /// </summary>
    [Fact]
    public void ScaleResolver_SortArrayOfDatetimeObjects()
    {
        // Arrange
        const string Rows = """[{"d":"2024-01-20"},{"d":"2024-02-05"},{"d":"2024-12-25"}]""";

        // Act
        var sorted = Resolve(Spec("bar", """{"x":{"field":"d","type":"ordinal","timeUnit":"month","sort":["Dec", 1]},"y":{"aggregate":"count"}}""", Rows))["x"]!;
        var domain = Resolve(Spec("bar", """{"x":{"field":"d","type":"ordinal","timeUnit":"month","scale":{"domain":["Dec", 1, {"month":"Feb"}]}},"y":{"aggregate":"count"}}""", Rows))["x"]!;

        // Assert: the missing February is first
        Assert.Equal(
            [DataValue.FromDate(TimeTestSupport.Ms(2012, 2, 1)), DataValue.FromDate(TimeTestSupport.Ms(2012, 12, 1)), DataValue.FromDate(TimeTestSupport.Ms(2012, 1, 1))],
            sorted.DiscreteDomain);

        // Assert: the month names and numbers of a domain are the parts of the unit (December, January, February of 2012)
        Assert.Equal(
            [DataValue.FromDate(TimeTestSupport.Ms(2012, 12, 1)), DataValue.FromDate(TimeTestSupport.Ms(2012, 1, 1)), DataValue.FromDate(TimeTestSupport.Ms(2012, 2, 1))],
            domain.DiscreteDomain);
    }

    /// <summary>
    ///     Validates the color of a temporal field: a sequential scale over the milliseconds, the interpolator of Vega-Lite's ramp, and a <c>domainMid</c> timestamp
    ///     (which makes it diverging; Vega-Lite takes a number there, not a date-time object).
    /// </summary>
    [Fact]
    public void ScaleResolver_TemporalColor_IsSequentialOverMilliseconds()
    {
        // Act
        var color = Resolve(Spec("point", """{"x":{"field":"v","type":"quantitative"},"color":{"field":"d","type":"temporal"}}"""))["color"]!;
        var diverging = Resolve(Spec("point", "{\"color\":{\"field\":\"d\",\"type\":\"temporal\",\"scale\":{\"domainMid\":" + D(1, 6).ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "}}}"))["color"]!;
        var model = color.CreateModel(0);

        // Assert
        Assert.Equal(ScaleKind.Sequential, color.Kind);
        Assert.Equal((D(1, 1), D(1, 11)), (color.DomainStart, color.DomainEnd));
        Assert.NotNull(color.Interpolator);
        Assert.True(color.IsTemporalField);
        Assert.Null(color.NiceCount);

        // Assert: the ends are the ends of the interpolator; the middle date is in the middle
        var interpolate = color.Interpolator;
        Assert.Equal(interpolate(0).ToCss(), model.MapText(DataValue.FromDate(D(1, 1))));
        Assert.Equal(interpolate(1).ToCss(), model.MapText(DataValue.FromDate(D(1, 11))));
        Assert.Equal(interpolate(0.5).ToCss(), model.MapText(DataValue.FromDate(D(1, 6))));
        Assert.Null(model.MapText(DataValue.Null));

        // Assert: domainMid makes three entries
        Assert.Equal(ScaleKind.Diverging, diverging.Kind);
        Assert.Equal([D(1, 1), D(1, 6), D(1, 11)], diverging.Domain);
    }

    /// <summary>
    ///     Validates that the discretizing scale types of a temporal color are refused (Vega-Lite accepts time and utc only): the sequential scale stays, with its warning
    ///     reported by the feature catalog.
    /// </summary>
    [Fact]
    public void ScaleResolver_TemporalColorQuantile_Threshold_Quantize_FallBackToSequential()
    {
        // Act / Assert
        foreach (var type in new[] { "quantile", "quantize", "threshold", "bin-ordinal", "ordinal" })
        {
            var color = Resolve(Spec("point", "{\"color\":{\"field\":\"d\",\"type\":\"temporal\",\"scale\":{\"type\":\"" + type + "\"}}}"))["color"]!;
            Assert.Equal(ScaleKind.Sequential, color.Kind);
        }
    }

    /// <summary>
    ///     Validates the numeric channels of a temporal field: size, opacity, stroke width and angle get a time scale over their own ranges (the first date is the minimum), and
    ///     the shape a discrete one over the dates.
    /// </summary>
    [Fact]
    public void ScaleResolver_TemporalSize_And_Opacity_UseTheTimeScale()
    {
        // Act
        var scales = Resolve(Spec("point", """{"size":{"field":"d","type":"temporal"},"opacity":{"field":"d","type":"temporal"},"angle":{"field":"d","type":"temporal"},"shape":{"field":"d","type":"temporal"}}"""));
        var size = scales["size"]!;
        var opacity = scales["opacity"]!;
        var angle = scales["angle"]!;

        // Assert: kinds and the use of the channel's range
        Assert.Equal(ScaleKind.Time, size.Kind);
        Assert.Equal(ScaleKind.Time, opacity.Kind);
        Assert.Equal(ScaleKind.Time, angle.Kind);
        Assert.Equal(ScaleKind.Ordinal, scales["shape"]!.Kind);
        Assert.Equal(3, scales["shape"]!.DiscreteDomain.Count);
        Assert.Equal((0.3, 0.8), opacity.ValueRange);
        Assert.Equal((0d, 360d), angle.ValueRange);

        // Assert: the dates map to the ends of the range
        var sizeModel = size.CreateModel(100);
        Near(size.ValueRange!.Value.Start, sizeModel.Map(DataValue.FromDate(D(1, 1))));
        Near(size.ValueRange.Value.End, sizeModel.Map(DataValue.FromDate(D(1, 11))));
        Near(0.55, opacity.CreateModel(100).Map(DataValue.FromDate(D(1, 6))));
        Near(180, angle.CreateModel(100).Map(DataValue.FromDate(D(1, 6))));

        // Assert: a time size does not start at zero (the zero rule is for quantitative sizes)
        Assert.Equal(D(1, 1), size.DomainStart);
    }

    /// <summary>
    ///     Validates the ranges of a time scale: an explicit range and the range keywords, <c>rangeMin</c> and <c>rangeMax</c>, <c>reverse</c>, <c>clamp</c>, <c>round</c> and
    ///     the descending sort (which reverses the range of a continuous domain).
    /// </summary>
    [Fact]
    public void ScaleResolver_TimeScale_RangeReverseClampRound()
    {
        // Act
        ResolvedScale X(string scale, string extra = "") => Resolve(Spec("point", "{\"x\":{\"field\":\"d\",\"type\":\"temporal\"" + scale + extra + "}}"))["x"]!;

        var range = X(",\"scale\":{\"range\":[50, 250]}").CreateModel(400);
        var limits = X(",\"scale\":{\"rangeMin\":20,\"rangeMax\":380}").CreateModel(400);
        var reversed = X(",\"scale\":{\"reverse\":true}");
        var descending = X(string.Empty, ",\"sort\":\"descending\"");
        var clampRound = X(",\"scale\":{\"clamp\":true,\"round\":true}");

        // Assert: explicit range, then its limits
        Near(50, range.Map(DataValue.FromDate(D(1, 1))));
        Near(250, range.Map(DataValue.FromDate(D(1, 11))));
        Near(20, limits.Map(DataValue.FromDate(D(1, 1))));
        Near(380, limits.Map(DataValue.FromDate(D(1, 11))));

        // Assert: reverse flips the range, and so does a descending sort of a continuous domain
        Assert.True(reversed.Reverse);
        Near(400, reversed.CreateModel(400).Map(DataValue.FromDate(D(1, 1))));
        Assert.True(descending.Reverse);
        Near(400, descending.CreateModel(400).Map(DataValue.FromDate(D(1, 1))));

        // Assert: clamp and round reach the model; a date out of the domain is clamped, and a position is a whole number
        Assert.True(clampRound.Clamp);
        Assert.True(clampRound.Round);
        var model = clampRound.CreateModel(100);
        Assert.True(model.Clamp && model.Round);
        Near(100, model.Map(DataValue.FromDate(D(2, 1))));
        Near(35, model.Map(DataValue.FromDate(D(1, 1) + (3.5 * Day))));
    }

    /// <summary>
    ///     Validates a datum of a temporal channel: a date text is a date and is the whole domain.
    /// </summary>
    [Fact]
    public void ScaleResolver_Datum_Temporal()
    {
        // Act
        var x = Resolve(Spec("rule", """{"x":{"datum":"2024-02-01","type":"temporal"}}"""))["x"]!;
        var number = Resolve(Spec("rule", """{"x":{"datum":1706745600000,"type":"temporal"}}"""))["x"]!;

        // Assert: one instant
        Assert.Equal(ScaleKind.Time, x.Kind);
        Assert.Null(x.FieldPath);
        Assert.Equal((D(2, 1), D(2, 1)), (x.DomainStart, x.DomainEnd));
        Assert.Equal(D(2, 1), number.DomainStart);
    }

    /// <summary>
    ///     Validates the limit of an explicit domain: more date entries than <see cref="VegaLiteLimits.MaxScaleDomainSize"/> is a located render failure at the array that names the
    ///     limit, for a temporal field and for a discrete time-unit field, and exactly the limit is accepted.
    /// </summary>
    [Fact]
    public void ScaleResolver_ConfigAndSpecArraysRespectMaxScaleDomainSize()
    {
        // Arrange: the render limit is below the parse limit
        var limits = new VegaLiteLimits { MaxScaleDomainSize = 3 };

        // Act
        var temporal = Assert.Throws<VegaLiteRenderException>(() =>
            Resolve(Spec("point", """{"x":{"field":"d","type":"temporal","scale":{"domain":["2024-01-01","2024-01-02","2024-01-03","2024-01-04"]}}}"""), null, limits));
        var discrete = Assert.Throws<VegaLiteRenderException>(() =>
            Resolve(Spec("bar", """{"x":{"field":"d","type":"ordinal","timeUnit":"yearmonthdate","scale":{"domain":["2024-01-01","2024-01-02","2024-01-03","2024-01-04"]}}}"""), null, limits));
        var accepted = Resolve(Spec("point", """{"x":{"field":"d","type":"temporal","scale":{"domain":["2024-01-01","2024-01-02","2024-01-03"]}}}"""), null, limits)["x"]!;

        // Assert
        Assert.Equal("/encoding/x/scale/domain", temporal.Location.Pointer);
        Assert.Contains("the limit of 3", temporal.Message, StringComparison.Ordinal);
        Assert.Contains("MaxScaleDomainSize", temporal.Message, StringComparison.Ordinal);
        Assert.Equal("/encoding/x/scale/domain", discrete.Location.Pointer);
        Assert.Contains("the limit of 3", discrete.Message, StringComparison.Ordinal);
        Assert.Equal(3, accepted.Domain!.Count);

        // Assert: a discrete domain of dates drawn from the data is bounded too
        const string Rows = """[{"d":"2024-01-01"},{"d":"2024-01-02"},{"d":"2024-01-03"},{"d":"2024-01-04"}]""";
        var data = Assert.Throws<VegaLiteRenderException>(() => Resolve(Spec("point", """{"x":{"field":"d","type":"ordinal","timeUnit":"yearmonthdate"}}""", Rows), null, limits));
        Assert.Equal("/encoding/x", data.Location.Pointer);
    }

    /// <summary>
    ///     Validates that the resolution does not depend on the culture: the scales under the Turkish and the German cultures are the same as under the default.
    /// </summary>
    [Fact]
    public void ScaleResolver_Deterministic_UnderCultures()
    {
        // Arrange
        const string Json = """{"data":{"values":[{"d":"2024-01-10T13:00:00Z"},{"d":"2024-03-20T05:00:00Z"}]},"mark":"bar","encoding":{"x":{"field":"d","type":"temporal","timeUnit":"yearmonth","scale":{"nice":true,"domain":["2024-01-01",{"year":2024,"month":"Apr"}]}}}}""";
        (ScaleKind, double, double, double?) Describe() => Resolve(Json)["x"] is { } x ? (x.Kind, x.DomainStart, x.DomainEnd, x.NiceCount) : default;
        var baseline = Describe();

        // Act / Assert
        foreach (var culture in new[] { "tr-TR", "de-DE" })
        {
            Assert.Equal(baseline, ExpressionTestSupport.UnderCulture(culture, Describe));
        }

        Assert.Equal(ScaleKind.Time, baseline.Item1);
        Assert.Equal((D(1, 1), D(4, 1), 10d), (baseline.Item2, baseline.Item3, baseline.Item4));
    }
}
