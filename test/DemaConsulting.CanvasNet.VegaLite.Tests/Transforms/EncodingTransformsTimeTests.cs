using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Tests.Data;
using static DemaConsulting.CanvasNet.VegaLite.Tests.Transforms.TimeTestPipeline;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;

/// <summary>
///     Unit tests for the time units in <see cref="DemaConsulting.CanvasNet.VegaLite.Transforms.EncodingTransforms"/>: the order of the implicit stages (bin, time unit,
///     aggregate, stack), the dimensions of the aggregate, the calendar of the chart and the units that the pipeline hands to the scales. The expected numbers are derived by
///     hand (the counts of the rows, the first of each month).
/// </summary>
public class EncodingTransformsTimeTests
{
    /// <summary>The rows of six dates in 2024: two in January, three in February (the 29th is a leap day), one in March.</summary>
    private const string SixDates =
        "[{\"d\":\"2024-01-05T00:00:00Z\"},{\"d\":\"2024-01-20T12:00:00Z\"},{\"d\":\"2024-02-10T00:00:00Z\"},{\"d\":\"2024-02-11T00:00:00Z\"},{\"d\":\"2024-02-29T23:59:59.999Z\"},{\"d\":\"2024-03-01T00:00:00Z\"}]";

    /// <summary>
    ///     Builds a view specification.
    /// </summary>
    /// <param name="rows">The JSON array of rows.</param>
    /// <param name="mark">The mark.</param>
    /// <param name="encoding">The JSON text of the encoding object.</param>
    /// <returns>The specification text.</returns>
    private static string Spec(string rows, string mark, string encoding) =>
        "{\"data\":{\"values\":" + rows + "},\"mark\":\"" + mark + "\",\"encoding\":" + encoding + "}";

    /// <summary>
    ///     Validates that the units are computed before the aggregate: the six dates are counted per month in first-seen order (January 2, February 3, March 1), the
    ///     groups carry the start and the end of their month, and the unit is handed on for the scales.
    /// </summary>
    [Fact]
    public void EncodingTransforms_TimeUnitThenAggregate_CountsByMonth()
    {
        // Act
        var (plan, data) = Encode(Spec(SixDates, "bar", """{"x":{"field":"d","type":"temporal","timeUnit":"yearmonth"},"y":{"aggregate":"count","type":"quantitative"}}"""));

        // Assert
        Assert.Equal(["__count", "yearmonth_d", "yearmonth_d_end"], data.Main.Fields);
        Assert.Equal(["2", "3", "1"], Rows(data.Main, "__count"));
        Assert.Equal([TimeTestSupport.Ms(2024, 1, 1), TimeTestSupport.Ms(2024, 2, 1), TimeTestSupport.Ms(2024, 3, 1)], Dates(data.Main, "yearmonth_d"));
        Assert.Equal([TimeTestSupport.Ms(2024, 2, 1), TimeTestSupport.Ms(2024, 3, 1), TimeTestSupport.Ms(2024, 4, 1)], Dates(data.Main, "yearmonth_d_end"));
        Assert.Equal(["yearmonth_d"], data.TimeUnits.Keys);
        Assert.Equal(["year", "month"], data.TimeUnits["yearmonth_d"].Parts);
        Assert.False(plan.IsIdentity);
    }

    /// <summary>
    ///     Validates that a mark that does not span its unit aggregates by the start only: the end is not a dimension and the aggregated table has no end field.
    /// </summary>
    [Fact]
    public void EncodingTransforms_TimeUnitThenAggregate_PointHasNoEndField()
    {
        // Act
        var (_, data) = Encode(Spec(SixDates, "point", """{"x":{"field":"d","type":"temporal","timeUnit":"yearmonth"},"y":{"aggregate":"count","type":"quantitative"}}"""));

        // Assert
        Assert.Equal(["__count", "yearmonth_d"], data.Main.Fields);
        Assert.Equal(["2", "3", "1"], Rows(data.Main, "__count"));
    }

    /// <summary>
    ///     Validates Vega-Lite's order of the stages with a bin, a time unit, an aggregate and a stack together: the values 5, 15 and 15 fall into the bins [0, 10) and
    ///     [10, 20) of a step of 10, the dates are the months January, January and February of the reference year 2012, the three groups have one row each, and the two
    ///     layers of the second bin are stacked descending on y (February at the bottom).
    /// </summary>
    [Fact]
    public void EncodingTransforms_BinThenTimeUnitThenAggregate_ThenStack()
    {
        // Arrange
        const string Input = "[{\"v\":5,\"d\":\"2024-01-10T00:00:00Z\"},{\"v\":15,\"d\":\"2024-01-20T00:00:00Z\"},{\"v\":15,\"d\":\"2024-02-10T00:00:00Z\"}]";

        // Act
        var (_, data) = Encode(Spec(Input,
            "bar",
            """{"x":{"field":"v","type":"quantitative","bin":{"step":10}},"y":{"aggregate":"count","type":"quantitative"},"color":{"field":"d","type":"ordinal","timeUnit":"month"}}"""));

        // Assert
        Assert.Equal(["__count", "__count_end", "__count_start", "bin_step_10_v", "bin_step_10_v_end", "month_d"], data.Main.Fields);
        Assert.Equal(
            ["0|2012-01-01T00:00:00.000Z|0|1", "10|2012-01-01T00:00:00.000Z|1|2", "10|2012-02-01T00:00:00.000Z|0|1"],
            Rows(data.Main, "bin_step_10_v", "month_d", "__count_start", "__count_end"));
        Assert.Equal(["bin_step_10_v"], data.Bins.Keys);
        Assert.Equal(["month_d"], data.TimeUnits.Keys);
    }

    /// <summary>
    ///     Validates the stack of layers that are a time unit: for the category a the layers are February (0, 1) at the bottom and January (1, 3) on top (descending on y), for b
    ///     February (0, 1); the rows keep their first-seen order.
    /// </summary>
    [Fact]
    public void EncodingTransforms_StackByTimeUnitField()
    {
        // Arrange
        const string Input = "[{\"c\":\"a\",\"d\":\"2024-01-10T00:00:00Z\"},{\"c\":\"a\",\"d\":\"2024-01-20T00:00:00Z\"},{\"c\":\"a\",\"d\":\"2024-02-10T00:00:00Z\"},{\"c\":\"b\",\"d\":\"2024-02-11T00:00:00Z\"}]";

        // Act
        var (plan, data) = Encode(Spec(Input,
            "bar",
            """{"x":{"field":"c","type":"nominal"},"y":{"aggregate":"count","type":"quantitative"},"color":{"field":"d","type":"ordinal","timeUnit":"month"}}"""));

        // Assert
        Assert.Equal([("month_d", true)], plan.Stack!.Sort.Select(s => (s.Field, s.Descending)));
        Assert.Equal(
            ["a|2012-01-01T00:00:00.000Z|1|3", "a|2012-02-01T00:00:00.000Z|0|1", "b|2012-02-01T00:00:00.000Z|0|1"],
            Rows(data.Main, "c", "month_d", "__count_start", "__count_end"));
    }

    /// <summary>
    ///     Validates that the aggregate and the stack see the same fields when a scale needs the rows before the aggregate: the Raw table keeps the unit fields of the rows.
    /// </summary>
    [Fact]
    public void EncodingTransforms_SortByAggregate_KeepsTheUnitFieldsInTheRawTable()
    {
        // Act
        var (_, data) = Encode(Spec(
            SixDates,
            "bar",
            """{"x":{"field":"d","type":"ordinal","timeUnit":"month","sort":{"op":"count","field":"d"}},"y":{"aggregate":"count","type":"quantitative"}}"""));

        // Assert: six rows with the start and the end of their month in the Raw table, three groups in the Main table
        Assert.Equal(6, data.Raw.RowCount);
        Assert.Contains("month_d", data.Raw.Fields);
        Assert.Contains("month_d_end", data.Raw.Fields);
        Assert.Equal(3, data.Main.RowCount);
        Assert.NotSame(data.Raw, data.Main);
    }

    /// <summary>
    ///     Validates that the <c>maxbins</c> form is resolved against the data and the resolved unit is handed on: five days and five bins make the unit of the day,
    ///     whose name stays the one of the form as written.
    /// </summary>
    [Fact]
    public void EncodingTransforms_MaxBins_HandsOnTheResolvedUnit()
    {
        // Arrange
        const string Input = "[{\"d\":\"2024-03-01T00:00:00Z\"},{\"d\":\"2024-03-03T12:00:00Z\"},{\"d\":\"2024-03-06T00:00:00Z\"}]";

        // Act
        var (plan, data) = Encode(Spec(Input, "point", """{"x":{"field":"d","type":"temporal","timeUnit":{"maxbins":5}},"y":{"aggregate":"count","type":"quantitative"}}"""));

        // Assert: the three days are three groups
        Assert.Equal("timeunit_maxbins_5_d", plan.Channel("x")!.ValueField);
        Assert.Equal(["year", "month", "date"], data.TimeUnits["timeunit_maxbins_5_d"].Parts);
        Assert.Equal([TimeTestSupport.Ms(2024, 3, 1), TimeTestSupport.Ms(2024, 3, 3), TimeTestSupport.Ms(2024, 3, 6)], Dates(data.Main, "timeunit_maxbins_5_d"));
    }

    /// <summary>
    ///     Validates that the units follow the calendar of the chart: in a United States style zone the local day of 2024-03-10T12:00Z starts at 05:00Z and ends 23 hours later.
    /// </summary>
    [Fact]
    public void EncodingTransforms_TimeUnit_FollowsTheCalendarOfTheChart()
    {
        // Arrange
        const string Input = "[{\"d\":\"2024-03-10T12:00:00Z\"}]";
        const string Encoding = """{"x":{"field":"d","type":"temporal","timeUnit":"yearmonthdate"},"y":{"aggregate":"count","type":"quantitative"}}""";

        // Act
        var (_, utc) = Encode(Spec(Input, "bar", Encoding));
        var (_, zoned) = Encode(Spec(Input, "bar", Encoding), TimeTestSupport.UsLike());

        // Assert
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 10), Ms(utc.Main, 0, "yearmonthdate_d"));
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 10, 5), Ms(zoned.Main, 0, "yearmonthdate_d"));
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 11, 4), Ms(zoned.Main, 0, "yearmonthdate_d_end"));
    }

    /// <summary>
    ///     Validates that an encoding without a time unit hands on no units.
    /// </summary>
    [Fact]
    public void EncodingTransforms_WithoutTimeUnits_HasNoUnits()
    {
        // Act
        var (_, data) = Encode(Spec("[{\"a\":1}]", "point", """{"x":{"field":"a","type":"quantitative"}}"""));

        // Assert
        Assert.Empty(data.TimeUnits);
    }

    /// <summary>
    ///     Validates the budget: the work of the unit stage counts against the step budget of the call, and a failure is located at the <c>timeUnit</c> of the channel.
    /// </summary>
    [Fact]
    public void EncodingTransforms_TimeUnit_FailureIsLocatedAtTheChannelUnit()
    {
        // Arrange
        const string Json = "{\"data\":{\"sequence\":{\"start\":0,\"stop\":10000,\"as\":\"d\"}},\"mark\":\"point\",\"encoding\":{\"x\":{\"field\":\"d\",\"type\":\"temporal\",\"timeUnit\":\"yearmonth\"}}}";

        // Act
        var ex = Assert.Throws<VegaLiteRenderException>(() => Encode(Json, limits: new VegaLiteLimits { MaxEvaluationSteps = 100 }));

        // Assert
        Assert.Equal("/encoding/x/timeUnit", ex.Location.Pointer);
        Assert.Contains("MaxEvaluationSteps", ex.Message, StringComparison.Ordinal);
    }
}
