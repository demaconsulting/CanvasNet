using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.SpecModel;

/// <summary>
///     Tests of the validation of the time constructs of a specification: time units (string and object), date-time objects wherever one may stand, the interval
///     forms of <c>tickCount</c> and <c>nice</c>, the <c>timeUnit</c> transform, the mark's time unit band settings (and how the configuration reads the same settings), and the
///     predicates that are compiled into expressions.
/// </summary>
public class SpecParserTimeTests
{
    /// <summary>The local time unit names of Vega-Lite: the 11 single units and the 29 multi units.</summary>
    private static readonly string[] LocalUnits =
    [
        "year", "quarter", "month", "week", "day", "dayofyear", "date", "hours", "minutes", "seconds", "milliseconds",
        "yearquarter", "yearquartermonth", "yearmonth", "yearmonthdate", "yearmonthdatehours", "yearmonthdatehoursminutes",
        "yearmonthdatehoursminutesseconds", "yearweek", "yearweekday", "yearweekdayhours", "yearweekdayhoursminutes",
        "yearweekdayhoursminutesseconds", "yeardayofyear", "quartermonth", "monthdate", "monthdatehours", "monthdatehoursminutes",
        "monthdatehoursminutesseconds", "weekday", "weekdayhours", "weekdayhoursminutes", "weekdayhoursminutesseconds", "dayhours",
        "dayhoursminutes", "dayhoursminutesseconds", "hoursminutes", "hoursminutesseconds", "minutesseconds", "secondsmilliseconds",
    ];

    /// <summary>
    ///     Parses a specification with one position channel definition.
    /// </summary>
    /// <param name="definition">The JSON text of the definition of <c>x</c>.</param>
    /// <returns>The tree.</returns>
    private static SpecTree ParseX(string definition) =>
        SpecParser.Parse("{\"mark\":\"bar\",\"encoding\":{\"x\":" + definition + "}}", "t.json");

    /// <summary>
    ///     Asserts that a specification is refused, located at a pointer in the file.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="pointer">The expected pointer.</param>
    /// <returns>The exception.</returns>
    private static VegaLiteSpecException Fail(string json, string pointer)
    {
        var ex = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse(json, "t.json"));
        Assert.Equal(pointer, ex.Location.Pointer);
        Assert.Equal("t.json", ex.Location.File);
        Assert.True(ex.Location.Line >= 1 && ex.Location.Column >= 1);
        return ex;
    }

    /// <summary>
    ///     Validates that every Vega-Lite time unit name is accepted with the <c>utc</c> prefix too, without a warning.
    /// </summary>
    [Fact]
    public void SpecParser_TimeUnit_AcceptsEveryName()
    {
        // Arrange / Act / Assert
        Assert.Equal(40, LocalUnits.Length);
        foreach (var name in LocalUnits)
        {
            Assert.Empty(ParseX("{\"field\":\"d\",\"type\":\"temporal\",\"timeUnit\":\"" + name + "\"}").Warnings);
            Assert.Empty(ParseX("{\"field\":\"d\",\"type\":\"temporal\",\"timeUnit\":\"utc" + name + "\"}").Warnings);
        }
    }

    /// <summary>
    ///     Validates that a time unit that does not exist is a located error naming the text, in an encoding, a condition, a predicate and a transform.
    /// </summary>
    [Fact]
    public void SpecParser_TimeUnit_UnknownUnitIsLocated()
    {
        // Act
        var encoding = Fail("{\"mark\":\"bar\",\"encoding\":{\"x\":{\"field\":\"d\",\"type\":\"temporal\",\"timeUnit\":\"foo\"}}}", "/encoding/x/timeUnit");
        var condition = Fail("{\"mark\":\"point\",\"encoding\":{\"color\":{\"condition\":{\"test\":\"true\",\"field\":\"d\",\"timeUnit\":\"year-week\",\"type\":\"temporal\"},\"value\":\"red\"}}}", "/encoding/color/condition/timeUnit");
        var predicate = Fail("{\"mark\":\"point\",\"transform\":[{\"filter\":{\"field\":\"d\",\"timeUnit\":\"Month\",\"equal\":1}}]}", "/transform/0/filter/timeUnit");
        var transform = Fail("{\"mark\":\"point\",\"transform\":[{\"timeUnit\":\"foo\",\"field\":\"d\",\"as\":\"x\"}]}", "/transform/0/timeUnit");

        // Assert
        Assert.Contains("Unrecognized time unit 'foo'", encoding.Message, StringComparison.Ordinal);
        Assert.Contains("Unrecognized time unit 'year-week'", condition.Message, StringComparison.Ordinal);
        Assert.Contains("Unrecognized time unit 'Month'", predicate.Message, StringComparison.Ordinal);
        Assert.Contains("Unrecognized time unit 'foo'", transform.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the members of a time unit object: each wrong member is located at itself, an object without a unit or <c>maxbins</c> at the object, and
    ///     the valid forms are accepted.
    /// </summary>
    /// <param name="unit">The <c>timeUnit</c> value.</param>
    /// <param name="pointer">The expected pointer under <c>/encoding/x/timeUnit</c>, or null when the value is valid.</param>
    [Theory]
    [InlineData("""{"unit":"month","step":3}""", null)]
    [InlineData("""{"unit":"utcmonth"}""", null)]
    [InlineData("""{"unit":"month","utc":true,"step":2}""", null)]
    [InlineData("""{"maxbins":10}""", null)]
    [InlineData("""{"unit":"yearmonth","binned":true}""", null)]
    [InlineData("""{"unit":"month","step":0}""", "/step")]
    [InlineData("""{"unit":"month","step":-1}""", "/step")]
    [InlineData("""{"unit":"month","step":"2"}""", "/step")]
    [InlineData("""{"maxbins":0}""", "/maxbins")]
    [InlineData("""{"maxbins":"x"}""", "/maxbins")]
    [InlineData("""{"unit":"month","utc":1}""", "/utc")]
    [InlineData("""{"unit":"month","binned":"yes"}""", "/binned")]
    [InlineData("""{"unit":3}""", "/unit")]
    [InlineData("""{"unit":"foo"}""", "/unit")]
    [InlineData("""{"step":2}""", "")]
    [InlineData("""{}""", "")]
    [InlineData("3", "")]
    [InlineData("true", "")]
    public void SpecParser_TimeUnit_ObjectForm_ValidatesMembers(string unit, string? pointer)
    {
        // Arrange
        var json = "{\"mark\":\"bar\",\"encoding\":{\"x\":{\"field\":\"d\",\"type\":\"temporal\",\"timeUnit\":" + unit + "}}}";

        // Act / Assert
        if (pointer is null)
        {
            Assert.Empty(SpecParser.Parse(json).Warnings);
        }
        else
        {
            Fail(json, "/encoding/x/timeUnit" + pointer);
        }
    }

    /// <summary>
    ///     Validates that a member that a time unit does not define is the warning <c>vl-unknown-property</c> at its key, and that the unit still applies.
    /// </summary>
    [Fact]
    public void SpecParser_TimeUnit_UnknownMemberIsAWarning()
    {
        // Act
        var tree = ParseX("{\"field\":\"d\",\"type\":\"temporal\",\"timeUnit\":{\"unit\":\"month\",\"bogus\":1}}");

        // Assert
        var warning = Assert.Single(tree.Warnings);
        Assert.Equal("vl-unknown-property", warning.Id);
        Assert.Equal(FeatureReportKind.Warning, warning.Kind);
        Assert.Equal("/encoding/x/timeUnit/bogus", warning.Location.Pointer);
    }

    /// <summary>
    ///     Validates that a binned unit (a name or the flag) parses, and that the plan and the catalog treat it as the unit that is ignored and reported.
    /// </summary>
    [Fact]
    public void SpecParser_TimeUnit_Binned_ParsesAndIsReported()
    {
        // Act
        var named = ParseX("{\"field\":\"d\",\"type\":\"temporal\",\"timeUnit\":\"binnedyearmonth\"}");
        var flagged = ParseX("{\"field\":\"d\",\"type\":\"temporal\",\"timeUnit\":{\"unit\":\"yearmonth\",\"binned\":true}}");

        // Assert
        Assert.Equal([("vl-timeunit-binned", "/encoding/x/timeUnit")], FeatureCatalog.Visit(named).Select(r => (r.Id, r.Location.Pointer)));
        Assert.Equal([("vl-timeunit-binned", "/encoding/x/timeUnit")], FeatureCatalog.Visit(flagged).Select(r => (r.Id, r.Location.Pointer)));
        Assert.Empty(EncodingPlan.Create(named.Root, ConfigResolver.Resolve(named, null)).TimeUnits);
    }

    /// <summary>
    ///     Validates the date-time objects wherever one may stand: a wrong member is located at the member (an unknown key, a month that is not one, a text
    ///     quarter), an empty object at the object, and valid objects are accepted.
    /// </summary>
    /// <param name="json">The specification.</param>
    /// <param name="pointer">The expected pointer, or null when the specification is valid.</param>
    [Theory]
    [InlineData("""{"mark":"bar","encoding":{"x":{"field":"d","type":"temporal","scale":{"domain":[{"year":2020,"month":"Mar"},{"year":2021,"quarter":2,"utc":true}]}}}}""", null)]
    [InlineData("""{"mark":"bar","encoding":{"x":{"field":"d","type":"temporal","scale":{"domainMin":{"year":2020},"domainMax":{"year":2021,"month":12,"date":31}}}}}""", null)]
    [InlineData("""{"mark":"bar","encoding":{"x":{"field":"d","type":"temporal","axis":{"values":[{"year":2020},{"year":2021}]}}}}""", null)]
    [InlineData("""{"mark":"point","encoding":{"color":{"field":"d","type":"temporal","legend":{"values":[{"year":2020}]}}}}""", null)]
    [InlineData("""{"mark":"point","transform":[{"filter":{"field":"d","oneOf":[{"year":2020},{"day":"Sunday"}]}}]}""", null)]
    [InlineData("""{"mark":"point","encoding":{"x":{"datum":{"year":2020,"month":"Mar"},"type":"temporal"}}}""", null)]
    [InlineData("""{"mark":"bar","encoding":{"x":{"field":"d","type":"temporal","scale":{"domain":[{"month":"Foo"}]}}}}""", "/encoding/x/scale/domain/0/month")]
    [InlineData("""{"mark":"bar","encoding":{"x":{"field":"d","type":"temporal","scale":{"domain":[{"quarter":"x"}]}}}}""", "/encoding/x/scale/domain/0/quarter")]
    [InlineData("""{"mark":"bar","encoding":{"x":{"field":"d","type":"temporal","scale":{"domain":[{"yr":1}]}}}}""", "/encoding/x/scale/domain/0/yr")]
    [InlineData("""{"mark":"bar","encoding":{"x":{"field":"d","type":"temporal","scale":{"domain":[{}]}}}}""", "/encoding/x/scale/domain/0")]
    [InlineData("""{"mark":"bar","encoding":{"x":{"field":"d","type":"temporal","scale":{"domainMin":{"year":"x"}}}}}""", "/encoding/x/scale/domainMin/year")]
    [InlineData("""{"mark":"bar","encoding":{"x":{"field":"d","type":"temporal","scale":{"domainMax":{"day":"Someday"}}}}}""", "/encoding/x/scale/domainMax/day")]
    [InlineData("""{"mark":"bar","encoding":{"x":{"field":"d","type":"temporal","axis":{"values":[{"hours":"x"}]}}}}""", "/encoding/x/axis/values/0/hours")]
    [InlineData("""{"mark":"point","encoding":{"color":{"field":"d","type":"temporal","legend":{"values":[{"utc":"yes"}]}}}}""", "/encoding/color/legend/values/0/utc")]
    [InlineData("""{"mark":"point","transform":[{"filter":{"field":"d","equal":{"month":"Foo"}}}]}""", "/transform/0/filter/equal/month")]
    [InlineData("""{"mark":"point","transform":[{"filter":{"field":"d","range":[{"year":2020},{"year":"x"}]}}]}""", "/transform/0/filter/range/1/year")]
    [InlineData("""{"mark":"point","transform":[{"filter":{"field":"d","oneOf":[{"year":2020},{"quarter":"q"}]}}]}""", "/transform/0/filter/oneOf/1/quarter")]
    [InlineData("""{"mark":"point","transform":[{"filter":{"field":"d","gt":{"quarter":"x"}}}]}""", "/transform/0/filter/gt/quarter")]
    [InlineData("""{"mark":"point","encoding":{"x":{"datum":{"year":2020,"month":"Foo"},"type":"temporal"}}}""", "/encoding/x/datum/month")]
    [InlineData("""{"mark":"point","encoding":{"color":{"condition":{"test":"true","datum":{"month":"Foo"}},"value":"red"}}}""", "/encoding/color/condition/datum/month")]
    public void SpecParser_DateTimeObject_InDomainPredicateAndValues_Validated(string json, string? pointer)
    {
        // Act / Assert
        if (pointer is null)
        {
            Assert.Empty(SpecParser.Parse(json).Warnings);
        }
        else
        {
            Fail(json, pointer);
        }
    }

    /// <summary>
    ///     Validates that an object value that is an expression reference is not read as a date-time object, and that the object that stands for a repeat reference as the
    ///     <c>datum</c> is not checked (it has no date-time key).
    /// </summary>
    [Fact]
    public void SpecParser_DateTimeObject_ExpressionReferencesAndRepeatDatumsAreLeftAlone()
    {
        // Act
        var domain = ParseX("{\"field\":\"d\",\"type\":\"temporal\",\"scale\":{\"domainMin\":{\"expr\":\"1\"}}}");
        var datum = ParseX("{\"datum\":{\"repeat\":\"column\"},\"type\":\"temporal\"}");

        // Assert
        Assert.Empty(domain.Warnings);
        Assert.Empty(datum.Warnings);
        Assert.False(SpecParser.IsDateTimeShaped(datum.Root["encoding"]!["x"]!["datum"]!));
        Assert.True(SpecParser.IsDateTimeShaped(SpecParser.Parse("{\"mark\":\"point\",\"encoding\":{\"x\":{\"datum\":{\"hours\":3}}}}").Root["encoding"]!["x"]!["datum"]!));
    }

    /// <summary>
    ///     Validates the interval forms of <c>tickCount</c> and <c>nice</c>: one of the eight names or an object with an interval and a positive step is accepted,
    ///     a name that is not an interval, an object without a valid interval and a step that is not positive are located errors.
    /// </summary>
    /// <param name="json">The specification.</param>
    /// <param name="pointer">The expected pointer, or null when the specification is valid.</param>
    [Theory]
    [InlineData("""{"mark":"bar","encoding":{"x":{"field":"d","type":"temporal","axis":{"tickCount":"month"},"scale":{"nice":"week"}}}}""", null)]
    [InlineData("""{"mark":"bar","encoding":{"x":{"field":"d","type":"temporal","axis":{"tickCount":{"interval":"day","step":2}},"scale":{"nice":{"interval":"hour"}}}}}""", null)]
    [InlineData("""{"mark":"bar","encoding":{"x":{"field":"d","type":"temporal","axis":{"tickCount":5},"scale":{"nice":true}}}}""", null)]
    [InlineData("""{"mark":"bar","encoding":{"x":{"field":"d","type":"temporal","axis":{"tickCount":{"expr":"3"}},"scale":{"nice":10}}}}""", null)]
    [InlineData("""{"mark":"point","encoding":{"color":{"field":"d","type":"temporal","legend":{"tickCount":"year"}}}}""", null)]
    [InlineData("""{"mark":"bar","encoding":{"x":{"field":"d","type":"temporal","axis":{"tickCount":"fortnight"}}}}""", "/encoding/x/axis/tickCount")]
    [InlineData("""{"mark":"bar","encoding":{"x":{"field":"d","type":"temporal","axis":{"tickCount":{"interval":"month","step":0}}}}}""", "/encoding/x/axis/tickCount/step")]
    [InlineData("""{"mark":"bar","encoding":{"x":{"field":"d","type":"temporal","axis":{"tickCount":{"interval":"foo","step":1}}}}}""", "/encoding/x/axis/tickCount/interval")]
    [InlineData("""{"mark":"bar","encoding":{"x":{"field":"d","type":"temporal","axis":{"tickCount":{"step":2}}}}}""", "/encoding/x/axis/tickCount")]
    [InlineData("""{"mark":"bar","encoding":{"x":{"field":"d","type":"temporal","axis":{"tickCount":{"interval":"month","step":"1"}}}}}""", "/encoding/x/axis/tickCount/step")]
    [InlineData("""{"mark":"point","encoding":{"color":{"field":"d","type":"temporal","legend":{"tickCount":"Month"}}}}""", "/encoding/color/legend/tickCount")]
    [InlineData("""{"mark":"bar","encoding":{"x":{"field":"d","type":"temporal","scale":{"nice":"fortnight"}}}}""", "/encoding/x/scale/nice")]
    [InlineData("""{"mark":"bar","encoding":{"x":{"field":"d","type":"temporal","scale":{"nice":{"interval":"month","step":-2}}}}}""", "/encoding/x/scale/nice/step")]
    public void SpecParser_TickCountInterval_Validated(string json, string? pointer)
    {
        // Act / Assert
        if (pointer is null)
        {
            Assert.Empty(SpecParser.Parse(json).Warnings);
        }
        else
        {
            Fail(json, pointer);
        }
    }

    /// <summary>
    ///     Validates that the eight interval names are the ones the check accepts.
    /// </summary>
    [Fact]
    public void SpecParser_IsTimeInterval_NamesTheEightIntervals()
    {
        // Act / Assert
        Assert.All(["millisecond", "second", "minute", "hour", "day", "week", "month", "year"], name => Assert.True(SpecParser.IsTimeInterval(name)));
        Assert.All(["milliseconds", "date", "quarter", "utcmonth", "Month", ""], name => Assert.False(SpecParser.IsTimeInterval(name)));
    }

    /// <summary>
    ///     Validates that the time unit band settings of a mark are numbers in the specification (a text or a boolean is an error at the value), that the configuration reads the
    ///     same settings leniently (a value that is not a number is ignored, so the mark has no band), and that a number in either place gives a mark that spans its time unit.
    /// </summary>
    [Fact]
    public void SpecParser_MarkTimeUnitBand_NonNumberIsAnErrorInTheSpecAndIgnoredInTheCallerConfig()
    {
        // Arrange
        const string Encoding = "{\"x\":{\"field\":\"d\",\"type\":\"temporal\",\"timeUnit\":\"yearmonth\"}}";
        static bool HasBandEnd(string mark, string? config)
        {
            var tree = SpecParser.Parse("{\"mark\":" + mark + ",\"encoding\":" + Encoding + "}");
            return EncodingPlan.Create(tree.Root, ConfigResolver.Resolve(tree, config)).Channel("x")!.HasBandEnd;
        }

        // Act: the specification
        var position = Fail("{\"mark\":{\"type\":\"bar\",\"timeUnitBandPosition\":\"middle\"},\"encoding\":" + Encoding + "}", "/mark/timeUnitBandPosition");
        var size = Fail("{\"mark\":{\"type\":\"bar\",\"timeUnitBandSize\":true},\"encoding\":" + Encoding + "}", "/mark/timeUnitBandSize");
        var accepted = SpecParser.Parse("{\"mark\":{\"type\":\"bar\",\"timeUnitBandPosition\":0.25,\"timeUnitBandSize\":0.5},\"encoding\":" + Encoding + "}");

        // Assert: the specification refuses, the configuration ignores
        Assert.Contains("timeUnitBandPosition", position.Message, StringComparison.Ordinal);
        Assert.Contains("timeUnitBandSize", size.Message, StringComparison.Ordinal);
        Assert.Empty(accepted.Warnings);
        Assert.True(HasBandEnd("\"bar\"", null));
        Assert.False(HasBandEnd("\"bar\"", "{\"bar\":{\"timeUnitBandPosition\":\"middle\"}}"));
        Assert.False(HasBandEnd("\"bar\"", "{\"bar\":{\"timeUnitBandPosition\":true}}"));
        Assert.True(HasBandEnd("\"bar\"", "{\"bar\":{\"timeUnitBandPosition\":0.25}}"));
        Assert.False(HasBandEnd("\"point\"", null));
        Assert.True(HasBandEnd("\"point\"", "{\"mark\":{\"timeUnitBandPosition\":0.25}}"));
        Assert.False(HasBandEnd("\"point\"", "{\"mark\":{\"timeUnitBandPosition\":\"x\"}}"));
        Assert.True(HasBandEnd("{\"type\":\"point\",\"timeUnitBandPosition\":0.5}", null));
    }

    /// <summary>
    ///     Validates the <c>timeUnit</c> transform: the unit, a field path and a non-empty text <c>as</c> are needed, and each problem is located.
    /// </summary>
    /// <param name="transform">The transform entry.</param>
    /// <param name="pointer">The expected pointer, or null when the entry is valid.</param>
    [Theory]
    [InlineData("""{"timeUnit":"yearmonth","field":"d","as":"m"}""", null)]
    [InlineData("""{"timeUnit":{"unit":"month","step":3},"field":"a.b","as":"q"}""", null)]
    [InlineData("""{"timeUnit":{"maxbins":10},"field":"d","as":"b"}""", null)]
    [InlineData("""{"timeUnit":"yearmonth","as":"m"}""", "/transform/0")]
    [InlineData("""{"timeUnit":"yearmonth","field":"a[","as":"m"}""", "/transform/0/field")]
    [InlineData("""{"timeUnit":"yearmonth","field":3,"as":"m"}""", "/transform/0/field")]
    [InlineData("""{"timeUnit":"yearmonth","field":"d"}""", "/transform/0")]
    [InlineData("""{"timeUnit":"yearmonth","field":"d","as":""}""", "/transform/0/as")]
    [InlineData("""{"timeUnit":"yearmonth","field":"d","as":3}""", "/transform/0/as")]
    [InlineData("""{"timeUnit":null,"field":"d","as":"m"}""", "/transform/0/timeUnit")]
    [InlineData("""{"timeUnit":3,"field":"d","as":"m"}""", "/transform/0/timeUnit")]
    [InlineData("""{"timeUnit":"foo","field":"d","as":"m"}""", "/transform/0/timeUnit")]
    public void SpecParser_TimeUnitTransform_ValidatesFieldAndAs(string transform, string? pointer)
    {
        // Arrange
        var json = "{\"mark\":\"point\",\"transform\":[" + transform + "]}";

        // Act / Assert
        if (pointer is null)
        {
            Assert.Empty(SpecParser.Parse(json).Warnings);
        }
        else
        {
            Fail(json, pointer);
        }
    }

    /// <summary>
    ///     Validates that the predicates written with the time functions are compiled when the specification is parsed (under the predicate object, also inside a
    ///     composition), that a plain predicate is not, and that a predicate that refers to an undeclared parameter through an expression value fails at the predicate.
    /// </summary>
    [Fact]
    public void SpecParser_TimePredicates_AreCompiledOnceUnderThePredicate()
    {
        // Arrange
        const string Json = """
            { "mark": "point", "transform": [
              { "filter": { "field": "d", "timeUnit": "month", "equal": 3 } },
              { "filter": { "and": [ { "field": "d", "gte": { "year": 2024 } }, { "field": "q", "gt": 1 } ] } },
              { "filter": { "field": "q", "gt": 1 } },
              { "filter": { "field": "d", "equal": { "expr": "1 + 1" } } } ] }
            """;

        // Act
        var tree = SpecParser.Parse(Json);
        var transforms = tree.Root["transform"]!.Items;

        // Assert: the month predicate, the date-time leaf and the expression value are compiled; the plain comparisons are evaluated natively
        Assert.NotNull(tree.Expressions.Get(transforms[0]["filter"]!));
        Assert.NotNull(tree.Expressions.Get(transforms[1]["filter"]!["and"]!.Items[0]));
        Assert.Null(tree.Expressions.Get(transforms[1]["filter"]!["and"]!.Items[1]));
        Assert.Null(tree.Expressions.Get(transforms[2]["filter"]!));
        Assert.NotNull(tree.Expressions.Get(transforms[3]["filter"]!));
        Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse("{\"mark\":\"point\",\"transform\":[{\"filter\":{\"field\":\"d\",\"equal\":{\"expr\":\"nope + 1\"}}}]}"));
    }

    /// <summary>
    ///     Validates that a list of dates that is longer than the domain size limit is refused at the list, and a list of the limit is accepted; and that the expression of a
    ///     predicate is bound by the expression length limit.
    /// </summary>
    [Fact]
    public void SpecParser_TimePredicates_OneOfIsBoundedByTheDomainSizeAndTheExpressionLength()
    {
        // Arrange
        const string Three = """{ "mark": "point", "transform": [ { "filter": { "field": "d", "oneOf": [ { "year": 2020 }, { "year": 2021 }, { "year": 2022 } ] } } ] }""";
        const string Two = """{ "mark": "point", "transform": [ { "filter": { "field": "d", "oneOf": [ { "year": 2020 }, { "year": 2021 } ] } } ] }""";

        // Act
        var ex = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse(Three, "t.json", new VegaLiteLimits { MaxScaleDomainSize = 2 }));
        var accepted = SpecParser.Parse(Two, "t.json", new VegaLiteLimits { MaxScaleDomainSize = 2 });
        var tooLong = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse(Two, "t.json", new VegaLiteLimits { MaxExpressionLength = 40 }));

        // Assert
        Assert.Equal("/transform/0/filter/oneOf", ex.Location.Pointer);
        Assert.Contains("MaxScaleDomainSize", ex.Message, StringComparison.Ordinal);
        Assert.Empty(accepted.Warnings);
        Assert.Equal("/transform/0/filter", tooLong.Location.Pointer);
    }

    /// <summary>
    ///     Validates the limit of the time format specifiers of a specification: the <c>format</c> of an axis or legend of a temporal field (a text, or a member of a multi-format
    ///     object), of a field with a time unit and of a field forced to time by <c>formatType</c>, and the date format of a <c>format.parse</c> entry, are refused at the format when
    ///     they are longer than 1,024 characters or expand to more than 4,096 pieces; a number format, a custom format type and a specifier at the limit are not.
    /// </summary>
    [Fact]
    public void SpecParser_TimeFormat_SpecifierLimitsAreSpecificationErrors()
    {
        // Arrange
        var tooLong = new string('x', 1_025);
        var longest = new string('x', 1_024);
        var manyPieces = string.Concat(Enumerable.Repeat("%c", 400));
        static string X(string members, string field = "\"type\":\"temporal\"") => "{\"mark\":\"point\",\"encoding\":{\"x\":{\"field\":\"d\"," + field + "," + members + "}}}";

        // Act
        var axisText = Fail(X("\"axis\":{\"format\":\"" + tooLong + "\"}"), "/encoding/x/axis/format");
        var axisPieces = Fail(X("\"axis\":{\"format\":\"" + manyPieces + "\"}"), "/encoding/x/axis/format");
        var axisMulti = Fail(X("\"axis\":{\"format\":{\"year\":\"" + tooLong + "\"}}"), "/encoding/x/axis/format/year");
        var legend = Fail("{\"mark\":\"point\",\"encoding\":{\"color\":{\"field\":\"d\",\"type\":\"temporal\",\"legend\":{\"format\":\"" + tooLong + "\"}}}}", "/encoding/color/legend/format");
        var unit = Fail(X("\"axis\":{\"format\":\"" + tooLong + "\"}", "\"type\":\"ordinal\",\"timeUnit\":\"month\""), "/encoding/x/axis/format");
        var forced = Fail(X("\"axis\":{\"formatType\":\"utc\",\"format\":\"" + tooLong + "\"}", "\"type\":\"quantitative\""), "/encoding/x/axis/format");
        var parse = Fail("{\"data\":{\"values\":[{\"d\":\"x\"}],\"format\":{\"parse\":{\"d\":\"date:'" + tooLong + "'\"}}},\"mark\":\"point\"}", "/data/format/parse/d");

        // Assert: the messages name the limits
        Assert.All([axisText, axisMulti, legend, unit, forced, parse], e => Assert.Contains("the limit of 1024 characters", e.Message, StringComparison.Ordinal));
        Assert.Contains("the limit of 4096 pieces", axisPieces.Message, StringComparison.Ordinal);

        // Assert: the specifier at the limit, a number format, a custom format type and a field that is not read as a time are accepted
        Assert.Empty(SpecParser.Parse(X("\"axis\":{\"format\":\"" + longest + "\"}"), "t.json").Warnings);
        SpecParser.Parse(X("\"axis\":{\"formatType\":\"number\",\"format\":\"" + tooLong + "\"}"), "t.json");
        SpecParser.Parse(X("\"axis\":{\"formatType\":\"custom\",\"format\":\"" + tooLong + "\"}"), "t.json");
        SpecParser.Parse(X("\"axis\":{\"format\":\"" + tooLong + "\"}", "\"type\":\"quantitative\""), "t.json");
        SpecParser.Parse("{\"data\":{\"values\":[{\"d\":\"x\"}],\"format\":{\"parse\":{\"d\":\"date:'" + longest + "'\"}}},\"mark\":\"point\"}", "t.json");
    }
}
