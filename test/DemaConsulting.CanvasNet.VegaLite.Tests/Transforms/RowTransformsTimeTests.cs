using DemaConsulting.CanvasNet.VegaLite.Tests.Data;
using static DemaConsulting.CanvasNet.VegaLite.Tests.Transforms.TimeTestPipeline;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;

/// <summary>
///     Unit tests for the predicates of the <c>filter</c> transform that have a time unit or compare with a date-time object (they are evaluated as the expressions that
///     the specification parser compiled for them), and for the <c>timeUnit</c> transform in a transform list. The rows are written so that the kept ids can be read off by
///     hand from the dates.
/// </summary>
public class RowTransformsTimeTests
{
    /// <summary>
    ///     Filters the rows <c>{"id": i, "d": date}</c> with a predicate and lists the kept ids.
    /// </summary>
    /// <param name="dates">The dates (JSON texts of the values) of the rows, in order.</param>
    /// <param name="predicate">The JSON of the predicate.</param>
    /// <param name="zone">The zone of the chart, or null for UTC.</param>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <returns>The kept ids joined with a comma.</returns>
    private static string Kept(string[] dates, string predicate, TimeZoneInfo? zone = null, VegaLiteLimits? limits = null)
    {
        var rows = "[" + string.Join(",", dates.Select((d, i) => "{\"id\":" + i + ",\"d\":" + d + "}")) + "]";
        var table = Run(Spec(rows, "[{\"filter\":" + predicate + "}]"), zone, limits);
        return string.Join(",", Rows(table, "id"));
    }

    /// <summary>
    ///     Validates a month predicate: the rows in March of any year pass (2023-03-10, 2024-03-05), the others (April, the leap day of February) do not.
    /// </summary>
    [Fact]
    public void RowTransforms_Filter_TimeUnitEqualMonth() =>
        Assert.Equal(
            "0,1",
            Kept(
                ["\"2023-03-10T12:00:00Z\"", "\"2024-03-05T00:00:00Z\"", "\"2024-04-01T00:00:00Z\"", "\"2024-02-29T23:59:59Z\""],
                """{"field":"d","timeUnit":"month","equal":{"month":"Mar"}}"""));

    /// <summary>
    ///     Validates that the month predicate also takes the number of the month (3 is March) and a list of months: the values 3 and 4 pass the dates in March and April.
    /// </summary>
    [Fact]
    public void RowTransforms_Filter_TimeUnitNumbersAndOneOfMonths()
    {
        // Arrange
        string[] dates = ["\"2024-03-05T00:00:00Z\"", "\"2024-04-01T00:00:00Z\"", "\"2024-05-01T00:00:00Z\""];

        // Act / Assert
        Assert.Equal("0", Kept(dates, """{"field":"d","timeUnit":"month","equal":3}"""));
        Assert.Equal("0,1", Kept(dates, """{"field":"d","timeUnit":"month","oneOf":[3,"Apr"]}"""));
        Assert.Equal("1,2", Kept(dates, """{"field":"d","timeUnit":"month","gt":3}"""));
        Assert.Equal("0,1", Kept(dates, """{"field":"d","timeUnit":"month","range":[4,3]}"""));
    }

    /// <summary>
    ///     Validates a range of date-time objects without a unit, inclusive at both ends: 2024-02-01 to 2024-04-01. The rows around the ends are 2024-01-31T23:59:59.999Z (out),
    ///     2024-02-01T00:00:00Z (in), 2024-03-15 (in), 2024-04-01T00:00:00Z (in) and 2024-04-01T00:00:00.001Z (out).
    /// </summary>
    [Fact]
    public void RowTransforms_Filter_RangeOfDatetimeObjects_NoUnit() =>
        Assert.Equal(
            "1,2,3",
            Kept(
                ["\"2024-01-31T23:59:59.999Z\"", "\"2024-02-01T00:00:00Z\"", "\"2024-03-15T00:00:00Z\"", "\"2024-04-01T00:00:00Z\"", "\"2024-04-01T00:00:00.001Z\""],
                """{"field":"d","range":[{"year":2024,"month":2,"date":1},{"year":2024,"month":4,"date":1}]}"""));

    /// <summary>
    ///     Validates the comparisons with a date-time object without a unit, and that a null date never passes an ordered comparison.
    /// </summary>
    [Fact]
    public void RowTransforms_Filter_ComparisonsWithADatetimeObject()
    {
        // Arrange: month 3 is March
        string[] dates = ["\"2024-02-29T00:00:00Z\"", "\"2024-03-01T00:00:00Z\"", "\"2024-03-02T00:00:00Z\"", "null"];
        const string March = """{"year":2024,"month":3,"date":1}""";

        // Act / Assert
        Assert.Equal("1", Kept(dates, "{\"field\":\"d\",\"equal\":" + March + "}"));
        Assert.Equal("0", Kept(dates, "{\"field\":\"d\",\"lt\":" + March + "}"));
        Assert.Equal("0,1", Kept(dates, "{\"field\":\"d\",\"lte\":" + March + "}"));
        Assert.Equal("2", Kept(dates, "{\"field\":\"d\",\"gt\":" + March + "}"));
        Assert.Equal("1,2", Kept(dates, "{\"field\":\"d\",\"gte\":" + March + "}"));
        Assert.Equal("1,2", Kept(dates, "{\"field\":\"d\",\"range\":[" + March + ",null]}"));
        Assert.Equal("0,1", Kept(dates, "{\"field\":\"d\",\"range\":[null," + March + "]}"));
        Assert.Equal("1", Kept(dates, "{\"field\":\"d\",\"oneOf\":[" + March + ",{\"year\":2030}]}"));
        Assert.Equal("0,2,3", Kept(dates, "{\"not\":{\"field\":\"d\",\"equal\":" + March + "}}"));
    }

    /// <summary>
    ///     Validates a list of UTC dates of the month with a UTC unit and UTC date-time objects: the dates 2024-03-05T23:00Z, 2024-03-06T01:00Z and 2024-03-07T00:00Z have the
    ///     UTC dates 5, 6 and 7, so the list of 5 and 6 keeps the first two, in any zone.
    /// </summary>
    [Fact]
    public void RowTransforms_Filter_OneOfDays_Utc()
    {
        // Arrange
        string[] dates = ["\"2024-03-05T23:00:00Z\"", "\"2024-03-06T01:00:00Z\"", "\"2024-03-07T00:00:00Z\""];
        const string Predicate = """{"field":"d","timeUnit":"utcdate","oneOf":[{"date":5,"utc":true},{"date":6,"utc":true}]}""";

        // Act / Assert
        Assert.Equal("0,1", Kept(dates, Predicate));
        Assert.Equal("0,1", Kept(dates, Predicate, TimeTestSupport.Fixed(5, 30)));
    }

    /// <summary>
    ///     Validates that the zone of the chart decides the date of a local unit: at plus 5:30 the same three dates are the local dates 6, 6 and 7 (04:30, 06:30 and 05:30), so the
    ///     sixth passes the first two, while in UTC the dates are 5, 6 and 7 and only the second passes.
    /// </summary>
    [Fact]
    public void RowTransforms_Filter_ZoneChangesTheDate()
    {
        // Arrange
        string[] dates = ["\"2024-03-05T23:00:00Z\"", "\"2024-03-06T01:00:00Z\"", "\"2024-03-07T00:00:00Z\""];
        const string Predicate = """{"field":"d","timeUnit":"date","equal":6}""";

        // Act / Assert
        Assert.Equal("1", Kept(dates, Predicate));
        Assert.Equal("0,1", Kept(dates, Predicate, TimeTestSupport.Fixed(5, 30)));
    }

    /// <summary>
    ///     Validates that a date-time object written in the local zone is that zone's instant: 2024-03-05T00:00 at plus 5:30 is 2024-03-04T18:30Z.
    /// </summary>
    [Fact]
    public void RowTransforms_Filter_DatetimeObjectIsReadInTheChartZone()
    {
        // Arrange
        string[] dates = ["\"2024-03-04T18:30:00Z\"", "\"2024-03-05T00:00:00Z\""];
        const string Predicate = """{"field":"d","equal":{"year":2024,"month":3,"date":5}}""";

        // Act / Assert
        Assert.Equal("1", Kept(dates, Predicate));
        Assert.Equal("0", Kept(dates, Predicate, TimeTestSupport.Fixed(5, 30)));
        Assert.Equal("1", Kept(dates, """{"field":"d","equal":{"year":2024,"month":3,"date":5,"utc":true}}""", TimeTestSupport.Fixed(5, 30)));
    }

    /// <summary>
    ///     Validates that text dates are parsed implicitly for a predicate with a time unit or a date-time object, and that a text that is not a date never passes.
    /// </summary>
    [Fact]
    public void RowTransforms_Filter_UnparsedStringDatesAreParsedImplicitly()
    {
        // Arrange
        string[] dates = ["\"2024-03-05\"", "\"Mar 5, 2024\"", "\"not a date\"", "1709596800000"];

        // Act / Assert: the number is 2024-03-05T00:00Z in milliseconds
        Assert.Equal("0,1,3", Kept(dates, """{"field":"d","equal":{"year":2024,"month":3,"date":5}}"""));
    }

    /// <summary>
    ///     Validates that the expression of a predicate is charged to the step budget: many rows exhaust a small budget with the located limit message.
    /// </summary>
    [Fact]
    public void RowTransforms_Filter_ExpressionPredicateIsCharged()
    {
        // Arrange
        const string Json = "{\"data\":{\"sequence\":{\"start\":0,\"stop\":2000,\"as\":\"d\"}},\"transform\":[{\"filter\":{\"field\":\"d\",\"timeUnit\":\"month\",\"equal\":1}}],\"mark\":\"point\"}";

        // Act
        var table = Run(Json);
        var ex = Assert.Throws<VegaLiteRenderException>(() => Run(Json, limits: new VegaLiteLimits { MaxEvaluationSteps = 1000 }));

        // Assert: every number is a few milliseconds after the epoch, January 1970, so month 1 keeps all of them
        Assert.Equal(2000, table.RowCount);
        Assert.Equal("/transform/0/filter", ex.Location.Pointer);
        Assert.Contains("MaxEvaluationSteps", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a <c>valid</c> test with a time unit tests the field itself, natively.
    /// </summary>
    [Fact]
    public void RowTransforms_Filter_ValidWithATimeUnit() =>
        Assert.Equal("0,1", Kept(["\"2024-03-05\"", "0", "null"], """{"field":"d","timeUnit":"year","valid":true}"""));

    /// <summary>
    ///     Validates the <c>timeUnit</c> transform in a transform list and the aggregate that groups by its start: the dates are counted per month (January 2, February 3, March 1).
    /// </summary>
    [Fact]
    public void RowTransforms_TimeUnitTransform_ThenAggregate()
    {
        // Arrange
        const string Input = "[{\"d\":\"2024-01-05\"},{\"d\":\"2024-01-20\"},{\"d\":\"2024-02-10\"},{\"d\":\"2024-02-11\"},{\"d\":\"2024-02-29\"},{\"d\":\"2024-03-01\"}]";

        // Act
        var table = Run(Spec(Input,
            """[{"timeUnit":"yearmonth","field":"d","as":"m"},{"aggregate":[{"op":"count","as":"n"}],"groupby":["m"]}]"""));

        // Assert
        Assert.Equal(["2", "3", "1"], Rows(table, "n"));
        Assert.Equal([TimeTestSupport.Ms(2024, 1, 1), TimeTestSupport.Ms(2024, 2, 1), TimeTestSupport.Ms(2024, 3, 1)], Dates(table, "m"));
    }

    /// <summary>
    ///     Validates the transform under the zone of the chart: the local month of 2024-03-01T02:00Z at minus five hours is February, so it starts at 2024-02-01T05:00Z.
    /// </summary>
    [Fact]
    public void RowTransforms_TimeUnitTransform_UsesTheChartZone()
    {
        // Act
        var table = Run(Spec("[{\"d\":\"2024-03-01T02:00:00Z\"}]", """[{"timeUnit":"yearmonth","field":"d","as":"m"}]"""), TimeTestSupport.UsLike());

        // Assert
        Assert.Equal(TimeTestSupport.Ms(2024, 2, 1, 5), Ms(table, 0, "m"));
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 1, 5), Ms(table, 0, "m_end"));
    }
}
