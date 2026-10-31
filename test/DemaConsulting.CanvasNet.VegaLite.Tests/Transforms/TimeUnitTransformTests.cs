using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Tests.Data;
using DemaConsulting.CanvasNet.VegaLite.Tests.Expressions;
using DemaConsulting.CanvasNet.VegaLite.Transforms;
using static DemaConsulting.CanvasNet.VegaLite.Tests.Transforms.TimeTestPipeline;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;

/// <summary>
///     Unit tests for <see cref="TimeUnitTransform"/>: the start and the end of the time unit of a date, in UTC and in zones built in the test, the
///     <c>maxbins</c> form, null and invalid dates, the limits, the charging of the work, cancellation and determinism. The expected instants are
///     derived by hand from the Vega rules (the reference year 2012, the end one step of the smallest part later); the anchors are in the comments
///     (2024-03-01 is 1,709,251,200,000, 2012-01-01 is 1,325,376,000,000) and the other instants are computed with the base class library.
/// </summary>
public class TimeUnitTransformTests
{
    /// <summary>The milliseconds in a day.</summary>
    private const double Day = 86_400_000;

    /// <summary>2012-01-01T00:00:00Z: 2012-03-01 (1,330,560,000,000) minus January and February (31 + 29 days).</summary>
    private const double Jan2012 = 1_325_376_000_000;

    /// <summary>
    ///     Builds a specification that applies one <c>timeUnit</c> transform to the field <c>d</c> of the given rows.
    /// </summary>
    /// <param name="rows">The JSON array of rows.</param>
    /// <param name="unit">The JSON of the <c>timeUnit</c> value.</param>
    /// <param name="format">Extra members of the data object (for example a format), each preceded by a comma, or empty.</param>
    /// <returns>The specification text.</returns>
    private static string UnitSpec(string rows, string unit, string format = "") =>
        "{\"data\":{\"values\":" + rows + format + "},\"transform\":[{\"timeUnit\":" + unit + ",\"field\":\"d\",\"as\":\"u\"}],\"mark\":\"point\"}";

    /// <summary>
    ///     Validates the start and the end of <c>yearmonth</c> for 2024-03-17T10:20Z: the first of March (1,709,251,200,000) and the first of April,
    ///     31 days later (1,711,929,600,000); the table keeps its fields and rows and the two new fields are dates.
    /// </summary>
    [Fact]
    public void TimeUnitTransform_YearMonth_StartAndEnd()
    {
        // Act
        var table = Run(UnitSpec("[{\"d\":\"2024-03-17T10:20:00Z\",\"k\":1}]", "\"yearmonth\""));

        // Assert
        Assert.Equal(["d", "k", "u", "u_end"], table.Fields);
        Assert.Equal(1_709_251_200_000, Ms(table, 0, "u"));
        Assert.Equal(1_711_929_600_000, Ms(table, 0, "u_end"));
        Assert.Equal(1_711_929_600_000 - 1_709_251_200_000, 31 * Day);
        Assert.Equal(1, table.GetValue(0, "k").Number);
    }

    /// <summary>
    ///     Validates that a unit without a year uses the reference year 2012: <c>month</c> of March is 2012-03-01 (1,330,560,000,000) and ends
    ///     at 2012-04-01, 31 days later (1,333,238,400,000).
    /// </summary>
    [Fact]
    public void TimeUnitTransform_Month_UsesReferenceYear2012()
    {
        // Act
        var table = Run(UnitSpec("[{\"d\":\"2024-03-17T10:20:00Z\"}]", "\"month\""));

        // Assert
        Assert.Equal(1_330_560_000_000, Ms(table, 0, "u"));
        Assert.Equal(1_333_238_400_000, Ms(table, 0, "u_end"));
    }

    /// <summary>
    ///     Validates that the day of the week is the weekday of January 2012 (a year that starts on a Sunday): a Tuesday (2024-03-05) is 2012-01-03
    ///     (1,325,548,800,000) and the end is one day later.
    /// </summary>
    [Fact]
    public void TimeUnitTransform_Day_IsTheWeekdayOfJanuary2012()
    {
        // Act
        var table = Run(UnitSpec("[{\"d\":\"2024-03-05T13:04:09.007Z\"},{\"d\":\"2024-03-10T00:00:00Z\"}]", "\"day\""));

        // Assert: the Sunday is 2012-01-01 (1,325,376,000,000)
        Assert.Equal([1_325_548_800_000, Jan2012], Dates(table, "u"));
        Assert.Equal([1_325_548_800_000 + Day, Jan2012 + Day], Dates(table, "u_end"));
    }

    /// <summary>
    ///     Validates the time-of-day units for 2024-03-05T13:04:09.007Z on the reference date 2012-01-01: <c>hours</c> keeps 13:00, <c>minutes</c> 00:04,
    ///     <c>seconds</c> 00:00:09 and <c>milliseconds</c> 7 ms; each end is one step of its part later.
    /// </summary>
    /// <param name="unit">The unit.</param>
    /// <param name="startOffset">The offset of the start from 2012-01-01T00:00Z.</param>
    /// <param name="step">The length of the unit.</param>
    [Theory]
    [InlineData("hours", 13 * 3_600_000.0, 3_600_000.0)]
    [InlineData("minutes", 4 * 60_000.0, 60_000.0)]
    [InlineData("seconds", 9_000.0, 1_000.0)]
    [InlineData("milliseconds", 7.0, 1.0)]
    [InlineData("hoursminutes", (13 * 3_600_000.0) + (4 * 60_000.0), 60_000.0)]
    public void TimeUnitTransform_Hours_Minutes_Seconds_Milliseconds(string unit, double startOffset, double step)
    {
        // Act
        var table = Run(UnitSpec("[{\"d\":\"2024-03-05T13:04:09.007Z\"}]", "\"" + unit + "\""));

        // Assert
        Assert.Equal(Jan2012 + startOffset, Ms(table, 0, "u"));
        Assert.Equal(Jan2012 + startOffset + step, Ms(table, 0, "u_end"));
    }

    /// <summary>
    ///     Validates the quarter units: <c>yearquarter</c> of 2024-05-17 starts 2024-04-01 and ends 2024-07-01 (91 days later); <c>quarter</c> alone
    ///     starts 2012-04-01 (1,333,238,400,000) and ends 2012-07-01 (91 days later).
    /// </summary>
    [Fact]
    public void TimeUnitTransform_Quarter_YearQuarter_Ends()
    {
        // Act
        var yearQuarter = Run(UnitSpec("[{\"d\":\"2024-05-17T00:00:00Z\"}]", "\"yearquarter\""));
        var quarter = Run(UnitSpec("[{\"d\":\"2024-05-17T00:00:00Z\"}]", "\"quarter\""));

        // Assert: April 30 + May 31 + June 30 = 91 days
        Assert.Equal(1_711_929_600_000, Ms(yearQuarter, 0, "u"));
        Assert.Equal(1_711_929_600_000 + (91 * Day), Ms(yearQuarter, 0, "u_end"));
        Assert.Equal(1_333_238_400_000, Ms(quarter, 0, "u"));
        Assert.Equal(1_333_238_400_000 + (91 * Day), Ms(quarter, 0, "u_end"));
    }

    /// <summary>
    ///     Validates that a step applies to the last part only: with <c>{"unit": "month", "step": 3}</c> March floors to January and April stays April,
    ///     and the end is three months after the start (January 2012 to April 2012 is 31 + 29 + 31 = 91 days).
    /// </summary>
    [Fact]
    public void TimeUnitTransform_Step_ThreeMonths()
    {
        // Act
        var table = Run(UnitSpec("[{\"d\":\"2024-03-17T00:00:00Z\"},{\"d\":\"2024-04-02T00:00:00Z\"}]", "{\"unit\":\"month\",\"step\":3}"));

        // Assert
        Assert.Equal([Jan2012, Jan2012 + (91 * Day)], Dates(table, "u"));
        Assert.Equal([Jan2012 + (91 * Day), TimeTestSupport.Ms(2012, 7, 1)], Dates(table, "u_end"));
    }

    /// <summary>
    ///     Validates the <c>maxbins</c> form with a five-day extent and five bins: the target of one day is nearest to the day interval of Vega's bin table,
    ///     so every date starts at its own midnight and ends a day later.
    /// </summary>
    [Fact]
    public void TimeUnitTransform_MaxBins_Day()
    {
        // Act: the extent is 2024-03-01T00:00Z to 2024-03-06T00:00Z, 5 days, 5 bins
        var table = Run(UnitSpec(
            "[{\"d\":\"2024-03-01T00:00:00Z\"},{\"d\":\"2024-03-03T12:00:00Z\"},{\"d\":\"2024-03-06T00:00:00Z\"}]",
            "{\"maxbins\":5}"));

        // Assert
        Assert.Equal([1_709_251_200_000, 1_709_251_200_000 + (2 * Day), 1_709_251_200_000 + (5 * Day)], Dates(table, "u"));
        Assert.Equal([1_709_251_200_000 + Day, 1_709_251_200_000 + (3 * Day), 1_709_251_200_000 + (6 * Day)], Dates(table, "u_end"));
    }

    /// <summary>
    ///     Validates the <c>maxbins</c> form over a year with ten bins: the target of 36.6 days is nearer to a 30-day month than to three months, so the
    ///     bins are calendar months (2024-01-01, 2024-06-01 and 2025-01-01 start their own months).
    /// </summary>
    [Fact]
    public void TimeUnitTransform_MaxBins_Month()
    {
        // Act
        var table = Run(UnitSpec(
            "[{\"d\":\"2024-01-01T00:00:00Z\"},{\"d\":\"2024-06-15T00:00:00Z\"},{\"d\":\"2025-01-01T00:00:00Z\"}]",
            "{\"maxbins\":10}"));

        // Assert
        Assert.Equal([TimeTestSupport.Ms(2024, 1, 1), TimeTestSupport.Ms(2024, 6, 1), TimeTestSupport.Ms(2025, 1, 1)], Dates(table, "u"));
        Assert.Equal([TimeTestSupport.Ms(2024, 2, 1), TimeTestSupport.Ms(2024, 7, 1), TimeTestSupport.Ms(2025, 2, 1)], Dates(table, "u_end"));
    }

    /// <summary>
    ///     Validates that the unit chosen for a <c>maxbins</c> form is reported to the caller, and that a table without a valid date (and an empty table)
    ///     gets null fields and keeps the unit as it was written.
    /// </summary>
    [Fact]
    public void TimeUnitTransform_MaxBins_ResolvedUnitsAndEmptyTables()
    {
        // Arrange
        var (table, context, _, tree, _) = Prepare(UnitSpec("[{\"d\":\"2024-03-01T00:00:00Z\"},{\"d\":\"2024-03-06T00:00:00Z\"}]", "{\"maxbins\":5}"));
        var node = tree.Root["transform"]!.Items[0];
        var request = new TimeUnitRequest("d", "u", "u_end", TimeUnitSpec.TryRead(node["timeUnit"]!, out var spec) ? spec : throw new InvalidOperationException(), node);
        var resolved = new TimeUnitSpec[1];

        // Act
        TimeUnitTransform.Apply(table, [request], context, tree.Locate(node), resolved);
        var empty = Run(UnitSpec("[]", "{\"maxbins\":5}"));
        var nothing = Run(UnitSpec("[{\"d\":null},{\"d\":null}]", "{\"maxbins\":5}"));

        // Assert: a 5 day extent with 5 bins is the day unit of the bin table
        Assert.Equal(["year", "month", "date"], resolved[0].Parts);
        Assert.Equal(1, resolved[0].Step);
        Assert.Equal(0, empty.RowCount);
        Assert.Equal(["u", "u_end"], empty.Fields);
        Assert.Equal([double.NaN, double.NaN], Dates(nothing, "u"));
    }

    /// <summary>
    ///     Validates that a <c>maxbins</c> above the domain size limit is a located render error.
    /// </summary>
    [Fact]
    public void TimeUnitTransform_MaxBins_AboveTheLimitFails()
    {
        // Arrange
        var json = UnitSpec("[{\"d\":\"2024-03-01T00:00:00Z\"}]", "{\"maxbins\":50}");

        // Act
        var ex = Assert.Throws<VegaLiteRenderException>(() => Run(json, limits: new VegaLiteLimits { MaxScaleDomainSize = 10 }));

        // Assert
        Assert.Equal("/transform/0", ex.Location.Pointer);
        Assert.Contains("MaxScaleDomainSize", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a null, a boolean, a text that is not a date (parsing switched off), a number outside the years 1 to 9999 and a missing field give
    ///     null in both fields, while a number is a date in milliseconds (0 is 1970-01-01, whose month ends 31 days later).
    /// </summary>
    [Fact]
    public void TimeUnitTransform_NullAndInvalidAndStringDates_YieldNull()
    {
        // Arrange: the parse is switched off so that the texts stay texts
        const string Rows = "[{\"d\":null},{\"d\":true},{\"d\":\"2024-03-17\"},{\"d\":1e300},{\"e\":1},{\"d\":0},{\"d\":-1e15}]";

        // Act
        var table = Run(UnitSpec(Rows, "\"yearmonth\"", ",\"format\":{\"parse\":null}"));

        // Assert
        Assert.Equal([double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, 0, double.NaN], Dates(table, "u"));
        Assert.Equal([double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, 31 * Day, double.NaN], Dates(table, "u_end"));
        Assert.True(table.GetValue(0, "u").IsNull && table.GetValue(0, "u_end").IsNull);
    }

    /// <summary>
    ///     Validates that a text that the implicit date parse reads is a date, and that one it cannot read is null (with the parse warning of the loader).
    /// </summary>
    [Fact]
    public void TimeUnitTransform_ImplicitParseReadsTextDates()
    {
        // Act
        var table = Run(UnitSpec("[{\"d\":\"2024-03-17\"},{\"d\":\"not a date\"},{\"d\":\"Mar 17, 2024\"}]", "\"yearmonthdate\""));

        // Assert: the third is the legacy form of the same day
        Assert.Equal([TimeTestSupport.Ms(2024, 3, 17), double.NaN, TimeTestSupport.Ms(2024, 3, 17)], Dates(table, "u"));
    }

    /// <summary>
    ///     Validates that a binned unit does not floor: the value is the start and the end follows one step later.
    /// </summary>
    [Fact]
    public void TimeUnitTransform_BinnedUnit_KeepsTheValueAsTheStart()
    {
        // Act: 2024-03-01T00:00Z is the start of its month already
        var table = Run(UnitSpec("[{\"d\":\"2024-03-01T00:00:00Z\"}]", "\"binnedyearmonth\""));

        // Assert
        Assert.Equal(1_709_251_200_000, Ms(table, 0, "u"));
        Assert.Equal(1_711_929_600_000, Ms(table, 0, "u_end"));
    }

    /// <summary>
    ///     Validates that a <c>utc</c> unit ignores the zone of the chart while a local unit follows it: at plus 5:30, 2024-03-05T20:00Z is the sixth of March
    ///     (01:30), so <c>date</c> is 2012-01-06 at local midnight (2012-01-05T18:30Z) and <c>utcdate</c> is 2012-01-05T00:00Z.
    /// </summary>
    [Fact]
    public void TimeUnitTransform_Utc_ZoneIndependent()
    {
        // Arrange
        var zone = TimeTestSupport.Fixed(5, 30);
        const string Rows = "[{\"d\":\"2024-03-05T20:00:00Z\"}]";

        // Act
        var local = Run(UnitSpec(Rows, "\"date\""), zone);
        var utc = Run(UnitSpec(Rows, "\"utcdate\""), zone);
        var utcFlag = Run(UnitSpec(Rows, "{\"unit\":\"date\",\"utc\":true}"), zone);

        // Assert
        Assert.Equal(TimeTestSupport.Ms(2012, 1, 5, 18, 30), Ms(local, 0, "u"));
        Assert.Equal(TimeTestSupport.Ms(2012, 1, 6, 18, 30), Ms(local, 0, "u_end"));
        Assert.Equal(TimeTestSupport.Ms(2012, 1, 5), Ms(utc, 0, "u"));
        Assert.Equal(TimeTestSupport.Ms(2012, 1, 6), Ms(utc, 0, "u_end"));
        Assert.Equal(Ms(utc, 0, "u"), Ms(utcFlag, 0, "u"));
    }

    /// <summary>
    ///     Validates the local day across a daylight saving change in a United States style zone (standard time minus five hours): 2024-03-10T12:00Z is
    ///     08:00 local, the day starts at local midnight (05:00Z, still standard time) and ends at the next local midnight (2024-03-11T04:00Z): 23 hours.
    /// </summary>
    [Fact]
    public void TimeUnitTransform_Zone_LocalDayAcrossDst()
    {
        // Act
        var table = Run(UnitSpec("[{\"d\":\"2024-03-10T12:00:00Z\"}]", "\"yearmonthdate\""), TimeTestSupport.UsLike());

        // Assert
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 10, 5), Ms(table, 0, "u"));
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 11, 4), Ms(table, 0, "u_end"));
        Assert.Equal(23 * 3_600_000.0, Ms(table, 0, "u_end") - Ms(table, 0, "u"));
    }

    /// <summary>
    ///     Validates that several requests are written in one pass and that an output name replaces an existing field of that name.
    /// </summary>
    [Fact]
    public void TimeUnitTransform_SeveralRequestsAndReplacedFields()
    {
        // Arrange
        var (table, context, _, tree, _) = Prepare(UnitSpec("[{\"d\":\"2024-03-17T10:20:00Z\",\"u\":\"old\"}]", "\"yearmonth\""));
        var node = tree.Root["transform"]!.Items[0];
        TimeUnitSpec.TryParse("yearmonth", out var month, out _);
        TimeUnitSpec.TryParse("year", out var year, out _);
        var requests = new[]
        {
            new TimeUnitRequest("d", "u", "u_end", month!, node),
            new TimeUnitRequest("d", "y", "y_end", year!, node),
        };

        // Act
        var result = TimeUnitTransform.Apply(table, requests, context, tree.Locate(node));

        // Assert: u is replaced by a date, y is the year (2024-01-01 and 2025-01-01)
        Assert.Equal(["d", "u", "u_end", "y", "y_end"], result.Fields);
        Assert.Equal(1_709_251_200_000, Ms(result, 0, "u"));
        Assert.Equal(TimeTestSupport.Ms(2024, 1, 1), Ms(result, 0, "y"));
        Assert.Equal(TimeTestSupport.Ms(2025, 1, 1), Ms(result, 0, "y_end"));
    }

    /// <summary>
    ///     Validates the field limit: the two fields that the transform adds count against the maximum, and the failure is located at the transform.
    /// </summary>
    [Fact]
    public void TimeUnitTransform_FieldLimit_IsCheckedBeforeAllocation()
    {
        // Arrange: d plus u and u_end are three fields, the limit is two
        var json = UnitSpec("[{\"d\":\"2024-03-17T10:20:00Z\"}]", "\"yearmonth\"");

        // Act
        var ex = Assert.Throws<VegaLiteRenderException>(() => Run(json, limits: new VegaLiteLimits { MaxFieldCount = 2 }));

        // Assert
        Assert.Equal("/transform/0", ex.Location.Pointer);
        Assert.Contains("MaxFieldCount", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that an output name is checked against the string limit of the run (a limit lower than the one the specification was parsed with).
    /// </summary>
    [Fact]
    public void TimeUnitTransform_OutputNameIsCheckedAgainstTheStringLimit()
    {
        // Arrange: the name u is one character, so the limit of one character fails on u_end (the date is a number: no text longer than the limit is read)
        var json = UnitSpec("[{\"d\":0}]", "\"yearmonth\"");

        // Act
        var ex = Assert.Throws<VegaLiteRenderException>(() => Run(json, limits: new VegaLiteLimits { MaxStringLength = 1 }, parseWithDefaults: true));

        // Assert
        Assert.Equal("/transform/0", ex.Location.Pointer);
        Assert.Contains("MaxStringLength", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a canceled token stops the transform inside its row loop (the check is made at the first row of every batch of 1,024).
    /// </summary>
    [Fact]
    public void TimeUnitTransform_Cancellation_StopsTheRowLoop()
    {
        // Arrange
        using var source = new CancellationTokenSource();
        var (table, context, _, tree, _) = Prepare(
            "{\"data\":{\"sequence\":{\"start\":0,\"stop\":3000,\"as\":\"d\"}},\"transform\":[{\"timeUnit\":\"yearmonth\",\"field\":\"d\",\"as\":\"u\"}],\"mark\":\"point\"}",
            token: source.Token);
        var node = tree.Root["transform"]!.Items[0];
        TimeUnitSpec.TryParse("yearmonth", out var unit, out _);
        source.Cancel();

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => TimeUnitTransform.Apply(table, [new TimeUnitRequest("d", "u", "u_end", unit!, node)], context, tree.Locate(node)));
    }

    /// <summary>
    ///     Validates that the work of every row is charged to the step budget: 10,000 rows need 20,000 elements (2 per row in UTC), which is 312 steps, so a
    ///     budget of 100 steps fails with the located limit message and the default budget does not.
    /// </summary>
    [Fact]
    public void TimeUnitTransform_StepsAreCharged()
    {
        // Arrange
        const string Json = "{\"data\":{\"sequence\":{\"start\":0,\"stop\":10000,\"as\":\"d\"}},\"transform\":[{\"timeUnit\":\"yearmonthdate\",\"field\":\"d\",\"as\":\"u\"}],\"mark\":\"point\"}";

        // Act
        var table = Run(Json);
        var ex = Assert.Throws<VegaLiteRenderException>(() => Run(Json, limits: new VegaLiteLimits { MaxEvaluationSteps = 100 }));

        // Assert: the numbers 0 to 9999 are milliseconds after the epoch, all on 1970-01-01
        Assert.Equal(10_000, table.RowCount);
        Assert.Equal(0, Ms(table, 9_999, "u"));
        Assert.Equal("/transform/0", ex.Location.Pointer);
        Assert.Contains("MaxEvaluationSteps", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a zone costs more per row than UTC: the same table fits 100 steps in UTC but not in a zone (8 per row is 80,000 elements, 1,250 steps).
    /// </summary>
    [Fact]
    public void TimeUnitTransform_ZonedRowsCostMoreThanUtcRows()
    {
        // Arrange: 10,000 rows cost 20,000 elements in UTC (312 steps) and 80,000 in a zone (1,250 steps)
        const string Json = "{\"data\":{\"sequence\":{\"start\":0,\"stop\":10000,\"as\":\"d\"}},\"transform\":[{\"timeUnit\":\"yearmonthdate\",\"field\":\"d\",\"as\":\"u\"}],\"mark\":\"point\"}";
        var limits = new VegaLiteLimits { MaxEvaluationSteps = 600 };

        // Act
        var utc = Run(Json, limits: limits);
        var ex = Assert.Throws<VegaLiteRenderException>(() => Run(Json, TimeTestSupport.Fixed(1), limits));

        // Assert
        Assert.Equal(10_000, utc.RowCount);
        Assert.Contains("MaxEvaluationSteps", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the result is the same on every run and under other cultures (digits, month names and the Turkish dotted I play no part in a number of milliseconds).
    /// </summary>
    [Fact]
    public void TimeUnitTransform_Deterministic_AcrossRunsAndCultures()
    {
        // Arrange
        var json = UnitSpec("[{\"d\":\"2024-03-17T10:20:00Z\"},{\"d\":\"2024-12-31T23:59:59.999Z\"}]", "{\"unit\":\"yearmonth\"}");
        var expected = Dates(Run(json, TimeTestSupport.UsLike()), "u");

        // Act / Assert
        Assert.Equal(expected, Dates(Run(json, TimeTestSupport.UsLike()), "u"));
        Assert.Equal(expected, ExpressionTestSupport.UnderCulture("tr-TR", () => Dates(Run(json, TimeTestSupport.UsLike()), "u")));
        Assert.Equal(expected, ExpressionTestSupport.UnderCulture("de-DE", () => Dates(Run(json, TimeTestSupport.UsLike()), "u")));

        // The second row is 2024-12-31T23:59:59.999Z, which is 18:59 in December on the standard offset: the month starts at local midnight of December 1
        Assert.Equal(TimeTestSupport.Ms(2024, 12, 1, 5), expected[1]);
    }

    /// <summary>
    ///     Validates the argument checks.
    /// </summary>
    [Fact]
    public void TimeUnitTransform_Apply_RejectsNullArguments()
    {
        // Arrange
        var (table, context, _, tree, _) = Prepare(UnitSpec("[{\"d\":1}]", "\"year\""));
        var location = tree.Locate(tree.Root["transform"]!.Items[0]);

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => TimeUnitTransform.Apply(null!, [], context, location));
        Assert.Throws<ArgumentNullException>(() => TimeUnitTransform.Apply(table, null!, context, location));
        Assert.Throws<ArgumentNullException>(() => TimeUnitTransform.Apply(table, [], null!, location));
    }
}
