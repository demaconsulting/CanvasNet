using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Tests.Data;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Guides;

/// <summary>
///     Unit tests of the helpers that the axes and the legends of temporal fields share (<see cref="TimeLabels"/>): the format types, the interval requests of
///     <c>tickCount</c>, Vega-Lite's default tick count rule for a time unit, the reading of dates in <c>values</c>, and the label formats of a guide. The expectations are
///     derived by hand from the definitions: 2024-03-05 is a Tuesday, 1,709,596,800,000 ms is its midnight, and Vega's multi-format picks the coarsest boundary an instant is on.
/// </summary>
public class TimeLabelsTests
{
    /// <summary>The instant 2024-03-05T00:00Z in milliseconds, worked out by hand (day 19,787).</summary>
    private const double March5 = 1_709_596_800_000;

    /// <summary>
    ///     Creates the options of a label function.
    /// </summary>
    /// <param name="format">The JSON of the format, or null.</param>
    /// <param name="formatType">The format type, or null.</param>
    /// <param name="unit">The name of the time unit, or null.</param>
    /// <param name="defaultSpecifier">The default specifier of the guide, or null.</param>
    /// <param name="maxLength">The longest label allowed.</param>
    /// <returns>The options.</returns>
    private static TimeLabels.Options Options(string? format = null, string? formatType = null, string? unit = null, string? defaultSpecifier = null, int maxLength = 100)
    {
        TimeUnitSpec? spec = null;
        if (unit is not null)
        {
            Assert.True(TimeUnitSpec.TryParse(unit, out spec, out _));
        }

        var node = format is null ? null : ConfigNode.FromSpec(TimeTestSupport.Node(format));
        return new TimeLabels.Options(node, formatType, spec, defaultSpecifier, TimeCalendar.Utc, maxLength, "axis", default, default);
    }

    /// <summary>
    ///     Validates which format types are evaluated.
    /// </summary>
    /// <param name="formatType">The format type.</param>
    /// <param name="known">Whether it is evaluated.</param>
    [Theory]
    [InlineData("number", true)]
    [InlineData("time", true)]
    [InlineData("utc", true)]
    [InlineData("custom", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void TimeLabels_IsKnownFormatType_NumberTimeAndUtc(string? formatType, bool known) =>
        Assert.Equal(known, TimeLabels.IsKnownFormatType(formatType));

    /// <summary>
    ///     Validates the interval forms of <c>tickCount</c>: a name, <c>{interval, step}</c>, and what is not an interval (a number, an unknown name, an object without an
    ///     interval).
    /// </summary>
    [Fact]
    public void TimeLabels_IntervalRequest_ReadsNamesAndObjects()
    {
        // Act
        var month = TimeLabels.IntervalRequest(TimeTestSupport.Node("\"month\""));
        var week = TimeLabels.IntervalRequest(TimeTestSupport.Node("{ \"interval\": \"week\", \"step\": 2 }"));
        var quarter = TimeLabels.IntervalRequest(TimeTestSupport.Node("\"quarter\""));
        var utc = TimeLabels.IntervalRequest(TimeTestSupport.Node("\"utcday\""));

        // Assert
        Assert.Equal((TimeIntervalKind.Month, 0.0, false), (month!.Value.Interval!.Value, month.Value.Step, month.Value.Utc));
        Assert.Equal((TimeIntervalKind.Week, 2.0), (week!.Value.Interval!.Value, week.Value.Step));
        Assert.Equal((TimeIntervalKind.Month, 3.0), (quarter!.Value.Interval!.Value, quarter.Value.Step));
        Assert.True(utc!.Value.Utc);
        Assert.Null(TimeLabels.IntervalRequest(null));
        Assert.Null(TimeLabels.IntervalRequest(TimeTestSupport.Node("5")));
        Assert.Null(TimeLabels.IntervalRequest(TimeTestSupport.Node("\"decade\"")));
        Assert.Null(TimeLabels.IntervalRequest(TimeTestSupport.Node("{ \"step\": 2 }")));
    }

    /// <summary>
    ///     Validates Vega-Lite's tick count rule: a unit that is exactly a month, hours, a day or a quarter (also with a step and as UTC) leaves the default of ten to Vega; every
    ///     other unit, and no unit, has one tick for every 40 pixels.
    /// </summary>
    /// <param name="unit">The time unit name, or null.</param>
    /// <param name="expected">Whether the default is Vega's ten.</param>
    [Theory]
    [InlineData("month", true)]
    [InlineData("hours", true)]
    [InlineData("day", true)]
    [InlineData("quarter", true)]
    [InlineData("utcmonth", true)]
    [InlineData("yearmonth", false)]
    [InlineData("date", false)]
    [InlineData("year", false)]
    [InlineData("minutes", false)]
    [InlineData("hoursminutes", false)]
    [InlineData(null, false)]
    public void TimeLabels_UsesVegaDefaultCount_OnlyForSingleMonthHoursDayQuarter(string? unit, bool expected)
    {
        // Arrange
        TimeUnitSpec? spec = null;
        if (unit is not null)
        {
            Assert.True(TimeUnitSpec.TryParse(unit, out spec, out _));
        }

        // Act / Assert
        Assert.Equal(expected, TimeLabels.UsesVegaDefaultCount(spec));
    }

    /// <summary>
    ///     Validates the reading of a date in <c>values</c>: a timestamp (truncated), a date text, a date-time object, and what is not a date (a boolean, a null, a text that does
    ///     not parse, a timestamp outside the years 1 to 9999) is NaN.
    /// </summary>
    [Fact]
    public void TimeLabels_ReadInstant_NumbersTextsObjectsAndNonDates()
    {
        // Arrange
        var utc = TimeCalendar.Utc;
        var zone = TimeCalendar.Create(TimeTestSupport.Fixed(5, 30));

        // Act / Assert
        Assert.Equal(March5, TimeLabels.ReadInstant(TimeTestSupport.Node("1709596800000.9"), utc));
        Assert.Equal(March5, TimeLabels.ReadInstant(TimeTestSupport.Node("\"2024-03-05\""), utc));
        Assert.Equal(March5, TimeLabels.ReadInstant(TimeTestSupport.Node("{ \"year\": 2024, \"month\": 3, \"date\": 5 }"), utc));
        Assert.Equal(March5, TimeLabels.ReadInstant(TimeTestSupport.Node("{ \"year\": 2024, \"month\": \"Mar\", \"date\": 5, \"utc\": true }"), zone));

        // Assert: a date-time object is in the calendar of the scale (local midnight is 18:30Z the day before), a date-only text is UTC
        Assert.Equal(March5 - (5.5 * 3_600_000), TimeLabels.ReadInstant(TimeTestSupport.Node("{ \"year\": 2024, \"month\": 3, \"date\": 5 }"), zone));
        Assert.Equal(March5, TimeLabels.ReadInstant(TimeTestSupport.Node("\"2024-03-05\""), zone));
        Assert.True(double.IsNaN(TimeLabels.ReadInstant(TimeTestSupport.Node("true"), utc)));
        Assert.True(double.IsNaN(TimeLabels.ReadInstant(TimeTestSupport.Node("null"), utc)));
        Assert.True(double.IsNaN(TimeLabels.ReadInstant(TimeTestSupport.Node("\"nonsense\""), utc)));
        Assert.True(double.IsNaN(TimeLabels.ReadInstant(TimeTestSupport.Node("1e300"), utc)));
        Assert.True(double.IsNaN(TimeLabels.ReadInstant(TimeTestSupport.Node("[1]"), utc)));
    }

    /// <summary>
    ///     Validates the choice of the label format: the format text, the multi-format levels of an object, the unit specifier, the default of the guide, and Vega's
    ///     multi-format when nothing else is given.
    /// </summary>
    [Fact]
    public void TimeLabels_Create_ChoosesTheFormatInTheOrderOfVegaLite()
    {
        // Act
        var text = TimeLabels.Create(Options("\"%Y/%m/%d\"", unit: "yearmonth", defaultSpecifier: "%d"));
        var levels = TimeLabels.Create(Options("{ \"date\": \"%d.\" }", unit: "yearmonth"));
        var unit = TimeLabels.Create(Options(unit: "yearmonth", defaultSpecifier: "%d"));
        var fallback = TimeLabels.Create(Options(defaultSpecifier: "%b %d, %Y"));
        var multi = TimeLabels.Create(Options());

        // Assert: March 5, 2024 is a Tuesday that is not a week start, so the multi-format shows the weekday and the date
        Assert.Equal("2024/03/05", text(March5));
        Assert.Equal("05.", levels(March5));
        Assert.Equal("Mar 2024", unit(March5));
        Assert.Equal("Mar 05, 2024", fallback(March5));
        Assert.Equal("Tue 05", multi(March5));
        Assert.Equal("Invalid Date", multi(double.NaN));
    }

    /// <summary>
    ///     Validates the format types: <c>number</c> formats the milliseconds with the d3 number format (and the JavaScript text without a format), <c>utc</c> formats in UTC
    ///     whatever the calendar of the scale is, and an invalid number format is a located specification error.
    /// </summary>
    [Fact]
    public void TimeLabels_Create_FormatTypesNumberAndUtc()
    {
        // Arrange: a scale in a zone 5:30 hours ahead; March 5 00:00Z is 05:30 there
        var zoneOptions = Options("\"%H:%M\"") with { Calendar = TimeCalendar.Create(TimeTestSupport.Fixed(5, 30)) };

        // Act
        var number = TimeLabels.Create(Options("\",d\"", "number"));
        var plain = TimeLabels.Create(Options(formatType: "number"));
        var local = TimeLabels.Create(zoneOptions);
        var utc = TimeLabels.Create(zoneOptions with { FormatType = "utc" });
        var invalid = Assert.Throws<VegaLiteSpecException>(() => TimeLabels.Create(Options("\"%Q\"", "number")));

        // Assert
        Assert.Equal("1,709,596,800,000", number(March5));
        Assert.Equal("1709596800000", plain(March5));
        Assert.Equal("05:30", local(March5));
        Assert.Equal("00:00", utc(March5));
        Assert.Contains("axis format", invalid.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the length limit: a label of exactly the limit is made, one character more is a located render exception that names the limit, for time formats and for
    ///     numbers.
    /// </summary>
    [Fact]
    public void TimeLabels_Create_LabelsAreBoundedByTheLimit()
    {
        // Arrange: "Mar 05, 2024" has 12 characters, the number 13
        var fits = TimeLabels.Create(Options("\"%b %d, %Y\"", maxLength: 12));
        var tooLong = TimeLabels.Create(Options("\"%b %d, %Y\"", maxLength: 11));
        var numberTooLong = TimeLabels.Create(Options(formatType: "number", maxLength: 12));

        // Act / Assert
        Assert.Equal("Mar 05, 2024", fits(March5));
        var time = Assert.Throws<VegaLiteRenderException>(() => tooLong(March5));
        Assert.Contains("the limit of 11", time.Message, StringComparison.Ordinal);
        var number = Assert.Throws<VegaLiteRenderException>(() => numberTooLong(March5));
        Assert.Contains("the limit of 12", number.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the aggregate limit: the labels that one guide makes (time labels and number labels alike) are limited together to the same length, each function
    ///     counts its own labels, and the exception names the limit and says that the labels are together too long.
    /// </summary>
    [Fact]
    public void TimeLabels_Create_LabelsOfAGuideAreBoundedTogether()
    {
        // Arrange: "Mar 05, 2024" has 12 characters, so two labels are 24 of 30 and the third would be 36; the number of March5 has 13, so two are 26 and the third 39
        var time = TimeLabels.Create(Options("\"%b %d, %Y\"", maxLength: 30));
        var another = TimeLabels.Create(Options("\"%b %d, %Y\"", maxLength: 30));
        var exact = TimeLabels.Create(Options("\"%b %d, %Y\"", maxLength: 36));
        var number = TimeLabels.Create(Options(formatType: "number", maxLength: 30));

        // Act
        var first = time(March5);
        var second = time(March5);
        var timeError = Assert.Throws<VegaLiteRenderException>(() => time(March5));
        var afterwards = Assert.Throws<VegaLiteRenderException>(() => time(March5));
        var exactLabels = new[] { exact(March5), exact(March5), exact(March5) };
        var exactError = Assert.Throws<VegaLiteRenderException>(() => exact(March5));
        var independent = another(March5);
        _ = number(March5);
        _ = number(March5);
        var numberError = Assert.Throws<VegaLiteRenderException>(() => number(March5));

        // Assert: the third label is refused (and the refused label costs nothing, so the next one is refused too); three labels fill 36 exactly and the fourth is refused
        Assert.Equal(["Mar 05, 2024", "Mar 05, 2024"], new[] { first, second });
        Assert.Contains("are together longer than the limit of 30 characters", timeError.Message, StringComparison.Ordinal);
        Assert.Contains("are together longer than the limit of 30 characters", afterwards.Message, StringComparison.Ordinal);
        Assert.Equal(["Mar 05, 2024", "Mar 05, 2024", "Mar 05, 2024"], exactLabels);
        Assert.Contains("the limit of 36", exactError.Message, StringComparison.Ordinal);
        Assert.Equal("Mar 05, 2024", independent);
        Assert.Contains("are together longer than the limit of 30 characters", numberError.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a time format that is too long, or that expands to too many pieces, is a located specification exception when the label function is created, for a
    ///     format text and for a member of a multi-format object, and that a specifier of the longest length still works.
    /// </summary>
    [Fact]
    public void TimeLabels_Create_LongSpecifier_IsASpecificationError()
    {
        // Arrange
        var tooLong = new string('x', TimeFormat.MaxSpecifierLength + 1);
        var manyPieces = string.Concat(Enumerable.Repeat("%c", 400));

        // Act
        var text = Assert.Throws<VegaLiteSpecException>(() => TimeLabels.Create(Options("\"" + tooLong + "\"")));
        var pieces = Assert.Throws<VegaLiteSpecException>(() => TimeLabels.Create(Options("\"" + manyPieces + "\"")));
        var member = Assert.Throws<VegaLiteSpecException>(() => TimeLabels.Create(Options("{ \"year\": \"" + tooLong + "\" }")));
        var longest = TimeLabels.Create(Options("\"" + new string('x', TimeFormat.MaxSpecifierLength) + "\"", maxLength: 2_000));

        // Assert: the messages name the owner of the format and the limits; the longest specifier works
        Assert.All([text, member], e => Assert.Contains("axis time format is not valid", e.Message, StringComparison.Ordinal));
        Assert.All([text, member], e => Assert.Contains("the limit of 1024 characters", e.Message, StringComparison.Ordinal));
        Assert.Contains("the limit of 4096 pieces", pieces.Message, StringComparison.Ordinal);
        Assert.Equal(TimeFormat.MaxSpecifierLength, longest(March5).Length);
    }
}
