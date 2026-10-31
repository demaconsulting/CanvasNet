using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using static DemaConsulting.CanvasNet.VegaLite.Tests.Data.TimeTestSupport;
using static DemaConsulting.CanvasNet.VegaLite.Tests.Expressions.ExpressionTestSupport;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Expressions;

/// <summary>
///     Tests of the date and time functions of the expression function table: the constructors, the getters, the offsets
///     and sequences, the formatting and parsing functions and <c>toDate</c>. Every expected value is worked out by hand from
///     the Vega and JavaScript definitions (the instants come from the base class library, see <c>TimeTestSupport</c>),
///     the zones are built in the test, and no test reads the host's zone, clock or culture.
/// </summary>
public class ExpressionFunctionsTimeTests
{
    /// <summary>The anchor instant 2024-02-29T12:34:56.789Z (a Thursday, day 60 of a leap year) as an expression literal.</summary>
    private const string Anchor = "1709210096789";

    /// <summary>
    ///     Evaluates an expression in a calendar.
    /// </summary>
    /// <param name="text">The expression text.</param>
    /// <param name="calendar">The calendar of the local functions; UTC when null.</param>
    /// <param name="limits">The limits; the defaults when null.</param>
    /// <returns>The value.</returns>
    private static DataValue EvalIn(string text, TimeCalendar? calendar = null, VegaLiteLimits? limits = null) =>
        new ExpressionEvaluator(limits, CancellationToken.None, null, calendar).Evaluate(Compile(text, limits));

    /// <summary>
    ///     Evaluates an expression that must give a number and checks it.
    /// </summary>
    /// <param name="text">The expression text.</param>
    /// <param name="expected">The expected number; NaN expects NaN.</param>
    /// <param name="calendar">The calendar of the local functions; UTC when null.</param>
    private static void AssertNumber(string text, double expected, TimeCalendar? calendar = null)
    {
        var value = EvalIn(text, calendar);
        Assert.True(value.IsNumber, $"'{text}' gave {value.Kind}, not a number");
        Assert.True(
            expected.CompareTo(value.Number) == 0,
            $"'{text}': expected {expected} but was {value.Number}");
    }

    /// <summary>
    ///     Evaluates an expression that must give a date and checks it.
    /// </summary>
    /// <param name="text">The expression text.</param>
    /// <param name="expected">The expected milliseconds since the epoch.</param>
    /// <param name="calendar">The calendar of the local functions; UTC when null.</param>
    private static void AssertDate(string text, double expected, TimeCalendar? calendar = null)
    {
        var value = EvalIn(text, calendar);
        Assert.True(value.IsDate, $"'{text}' gave {value.Kind}, not a date");
        Assert.True(expected.CompareTo(value.Milliseconds) == 0, $"'{text}': expected {expected} but was {value.Milliseconds}");
    }

    /// <summary>
    ///     Evaluates an expression that must give text and checks it.
    /// </summary>
    /// <param name="text">The expression text.</param>
    /// <param name="expected">The expected text.</param>
    /// <param name="calendar">The calendar of the local functions; UTC when null.</param>
    private static void AssertText(string text, string expected, TimeCalendar? calendar = null)
    {
        var value = EvalIn(text, calendar);
        Assert.True(value.IsString, $"'{text}' gave {value.Kind}, not a string");
        Assert.Equal(expected, value.Text);
    }

    /// <summary>
    ///     Evaluates an expression that must give null.
    /// </summary>
    /// <param name="text">The expression text.</param>
    /// <param name="calendar">The calendar of the local functions; UTC when null.</param>
    private static void AssertNull(string text, TimeCalendar? calendar = null)
    {
        var value = EvalIn(text, calendar);
        Assert.True(value.IsNull, $"'{text}' gave {value.Kind}, not null");
    }

    /// <summary>
    ///     Validates that <c>datetime</c> with components reads a local date: the month counts from 0 (index 1 is February).
    /// </summary>
    [Fact]
    public void ExpressionFunctions_Datetime_Components_AreALocalDate()
    {
        // Act / Assert: the anchor 2024-02-29T12:34:56.789Z, and the defaults of the missing components
        AssertDate("datetime(2024, 1, 29, 12, 34, 56, 789)", AnchorMilliseconds);
        AssertDate("datetime(2024, 1)", Ms(2024, 2, 1));
        AssertDate("datetime(2024, 0, 15, 6)", Ms(2024, 1, 15, 6));
        AssertDate("datetime('2024', '2', '5')", Ms(2024, 3, 5));
        AssertDate("datetime(2024, 1.9, 29.9, 12.9, 34.9, 56.9, 789.9)", AnchorMilliseconds);
    }

    /// <summary>
    ///     Validates that the components roll over like the JavaScript Date constructor (MakeDay and MakeTime).
    /// </summary>
    [Fact]
    public void ExpressionFunctions_Datetime_ComponentsOverflow_RollIntoTheNextUnit()
    {
        // Act / Assert: month 12 is January of the next year, day 0 is the last day of the previous month
        AssertDate("datetime(2024, 12, 1)", Ms(2025, 1, 1));
        AssertDate("datetime(2024, -1, 1)", Ms(2023, 12, 1));
        AssertDate("datetime(2024, 0, 32)", Ms(2024, 2, 1));
        AssertDate("datetime(2024, 0, 0)", Ms(2023, 12, 31));
        AssertDate("datetime(2024, 0, 1, 24)", Ms(2024, 1, 2));
        AssertDate("datetime(2024, 0, 1, -1)", Ms(2023, 12, 31, 23));

        // Assert: Feb 29 12:00 + 61 min = 13:01, + 61 s = 13:02:01, + 1001 ms = 13:02:02.001; 36 h is Mar 1 12:00
        AssertDate("datetime(2024, 1, 29, 12, 61, 61, 1001)", Ms(2024, 2, 29, 13, 2, 2, 1));
        AssertDate("datetime(2024, 1, 29, 36, 61, 61, 1001)", Ms(2024, 3, 1, 13, 2, 2, 1));
    }

    /// <summary>
    ///     Validates the century rule of the Date constructor and the end of the valid range.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_Datetime_YearsBelowOneHundred_AreNineteenHundreds()
    {
        // Act / Assert: years 0 to 99 are 1900 to 1999, year 100 is year 100
        AssertDate("datetime(99, 0)", Ms(1999, 1, 1));
        AssertDate("datetime(0, 0)", Ms(1900, 1, 1));
        AssertDate("datetime(24, 0, 1)", Ms(1924, 1, 1));
        AssertDate("datetime(100, 0)", Ms(100, 1, 1));
        AssertDate("datetime(9999, 11, 31, 23, 59, 59, 999)", 253_402_300_799_999);

        // Assert: outside the years 1 to 9999 and non-finite components give no date
        AssertNull("datetime(10000, 0, 1)");
        AssertNull("datetime(9999, 11, 31, 23, 59, 59, 1000)");
        AssertNull("datetime(-1, 0, 1)");
        AssertNull("datetime(NaN, 0)");
        AssertNull("datetime(2024, Infinity)");
        AssertNull("datetime('x', 0)");
    }

    /// <summary>
    ///     Validates the one-argument forms of <c>datetime</c>: a number is milliseconds, a date is copied, a text is parsed
    ///     in the calendar, anything else is no date, and no argument is null (there is no clock).
    /// </summary>
    [Fact]
    public void ExpressionFunctions_Datetime_OneArgument_IsAnInstant()
    {
        // Act / Assert
        AssertDate("datetime(" + Anchor + ")", AnchorMilliseconds);
        AssertDate("datetime(1709210096789.9)", AnchorMilliseconds);
        AssertDate("datetime(datetime(2024, 1, 29, 12, 34, 56, 789))", AnchorMilliseconds);
        AssertDate("datetime('2024-03-05')", Ms(2024, 3, 5));
        AssertDate("datetime(0)", 0);
        AssertDate("datetime(-1)", -1);
        AssertNull("datetime()");
        AssertNull("datetime(null)");
        AssertNull("datetime(true)");
        AssertNull("datetime('not a date')");
        AssertNull("datetime('')");
        AssertNull("datetime(NaN)");
        AssertNull("datetime(1e300)");
        AssertNull("datetime(253402300800000)");
        AssertNull("datetime([1])");
    }

    /// <summary>
    ///     Validates that <c>datetime</c> reads the components and a date text in the zone of the calendar, and that a gap is
    ///     read with the offset from before the change.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_Datetime_UsesTheCalendarZone()
    {
        // Arrange: +05:30 and a US-like zone (daylight time from 2024-03-10T07:00Z)
        var fixedZone = Calendar(Fixed(5, 30));
        var us = Calendar(UsLike());

        // Act / Assert: local midnight of 2024-01-01 at +05:30 is 18:30 UTC of the day before
        AssertDate("datetime(2024, 0, 1)", Ms(2023, 12, 31, 18, 30), fixedZone);
        AssertDate("datetime('2024-01-01T00:00:00')", Ms(2023, 12, 31, 18, 30), fixedZone);
        AssertDate("datetime('2024-01-01')", Ms(2024, 1, 1), fixedZone);
        AssertDate("datetime('2024-01-01T00:00:00Z')", Ms(2024, 1, 1), fixedZone);

        // Assert: 02:30 on 2024-03-10 does not exist; it is read with the standard offset (-5 h), so it shows 03:30
        AssertDate("datetime(2024, 2, 10, 2, 30)", Ms(2024, 3, 10, 7, 30), us);

        // Assert: 01:30 on 2024-11-03 occurs twice and takes the earlier instant (daylight time, -4 h)
        AssertDate("datetime(2024, 10, 3, 1, 30)", Ms(2024, 11, 3, 5, 30), us);
    }

    /// <summary>
    ///     Validates <c>utc</c>: it returns the number of milliseconds (like <c>Date.UTC</c>), defaults the month, and is not
    ///     affected by the zone.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_Utc_ReturnsAMillisecondNumber()
    {
        // Arrange
        var fixedZone = Calendar(Fixed(5, 30));

        // Act / Assert
        AssertNumber("utc(2024, 1, 29, 12, 34, 56, 789)", AnchorMilliseconds);
        AssertNumber("utc(2024, 1, 29, 12, 34, 56, 789)", AnchorMilliseconds, fixedZone);
        AssertNumber("utc(2024)", Ms(2024, 1, 1));
        AssertNumber("utc(99, 0)", Ms(1999, 1, 1));
        AssertNumber("utc(2024, 12, 1)", Ms(2025, 1, 1));
        AssertNumber("utc()", double.NaN);
        AssertNumber("utc(10000)", double.NaN);
        AssertNumber("utc('x')", double.NaN);
        Assert.False(EvalIn("isDate(utc(2024, 0, 1))").Boolean);
        Assert.True(EvalIn("isDate(datetime(2024, 0, 1))").Boolean);
    }

    /// <summary>
    ///     Validates the getters at the anchor instant in UTC.
    /// </summary>
    /// <param name="text">The expression text.</param>
    /// <param name="expected">The expected number.</param>
    [Theory]
    [InlineData("year(" + Anchor + ")", 2024)]
    [InlineData("month(" + Anchor + ")", 1)]
    [InlineData("date(" + Anchor + ")", 29)]
    [InlineData("day(" + Anchor + ")", 4)]
    [InlineData("hours(" + Anchor + ")", 12)]
    [InlineData("minutes(" + Anchor + ")", 34)]
    [InlineData("seconds(" + Anchor + ")", 56)]
    [InlineData("milliseconds(" + Anchor + ")", 789)]
    [InlineData("time(" + Anchor + ")", 1_709_210_096_789)]
    [InlineData("utcyear(" + Anchor + ")", 2024)]
    [InlineData("utcmonth(" + Anchor + ")", 1)]
    [InlineData("utcdate(" + Anchor + ")", 29)]
    [InlineData("utcday(" + Anchor + ")", 4)]
    [InlineData("utchours(" + Anchor + ")", 12)]
    [InlineData("utcminutes(" + Anchor + ")", 34)]
    [InlineData("utcseconds(" + Anchor + ")", 56)]
    [InlineData("utcmilliseconds(" + Anchor + ")", 789)]
    [InlineData("quarter(" + Anchor + ")", 1)]
    [InlineData("utcquarter(" + Anchor + ")", 1)]
    [InlineData("week(" + Anchor + ")", 8)]
    [InlineData("utcweek(" + Anchor + ")", 8)]
    [InlineData("isoweek(" + Anchor + ")", 9)]
    [InlineData("utcisoweek(" + Anchor + ")", 9)]
    [InlineData("dayofyear(" + Anchor + ")", 60)]
    [InlineData("utcdayofyear(" + Anchor + ")", 60)]
    [InlineData("timezoneoffset(" + Anchor + ")", 0)]
    public void ExpressionFunctions_Getters_AtTheAnchorInstant(string text, double expected) =>
        AssertNumber(text, expected);

    /// <summary>
    ///     Validates the getters before the epoch, where the parts count back from the end of 1969, and the truncation of a
    ///     fraction of a millisecond.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_Getters_BeforeTheEpochAndWithFractions()
    {
        // Act / Assert: -1 ms is 1969-12-31T23:59:59.999Z, a Wednesday
        AssertNumber("year(-1)", 1969);
        AssertNumber("month(-1)", 11);
        AssertNumber("date(-1)", 31);
        AssertNumber("day(-1)", 3);
        AssertNumber("hours(-1)", 23);
        AssertNumber("milliseconds(-1)", 999);
        AssertNumber("milliseconds(1709210096789.9)", 789);
        AssertNumber("time(1709210096789.9)", AnchorMilliseconds);
        AssertNumber("time(-0.5)", 0);

        // Assert: the ends of the valid range
        AssertNumber("year(-62135596800000)", 1);
        AssertNumber("year(253402300799999)", 9999);
        AssertNumber("year(253402300800000)", double.NaN);
        AssertNumber("year(-62135596800001)", double.NaN);
    }

    /// <summary>
    ///     Validates that a value that is not an instant gives NaN from every getter, and that a date text is read.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_Getters_InvalidInstants_AreNaN()
    {
        // Act / Assert
        AssertNumber("year()", double.NaN);
        AssertNumber("year(null)", double.NaN);
        AssertNumber("year(NaN)", double.NaN);
        AssertNumber("year(true)", double.NaN);
        AssertNumber("year('bogus')", double.NaN);
        AssertNumber("year(1e300)", double.NaN);
        AssertNumber("time(datetime(10000, 0, 1))", double.NaN);
        AssertNumber("timezoneoffset('bogus')", double.NaN);
        AssertNumber("isoweek(null)", double.NaN);
        AssertNumber("year('2024-03-05')", 2024);
        AssertNumber("date('2024-03-05T10:00:00Z')", 5);
    }

    /// <summary>
    ///     Validates that the local getters use the calendar zone and the UTC getters do not: at 2024-02-29T20:00Z the
    ///     wall time at +05:30 is 01:30 on Friday 2024-03-01.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_Getters_LocalFormsFollowTheZone_UtcFormsDoNot()
    {
        // Arrange
        var zone = Calendar(Fixed(5, 30));
        var instant = Ms(2024, 2, 29, 20).ToString("0", System.Globalization.CultureInfo.InvariantCulture);

        // Act / Assert: local
        AssertNumber($"year({instant})", 2024, zone);
        AssertNumber($"month({instant})", 2, zone);
        AssertNumber($"date({instant})", 1, zone);
        AssertNumber($"day({instant})", 5, zone);
        AssertNumber($"hours({instant})", 1, zone);
        AssertNumber($"minutes({instant})", 30, zone);
        AssertNumber($"dayofyear({instant})", 61, zone);

        // Assert: UTC
        AssertNumber($"utcmonth({instant})", 1, zone);
        AssertNumber($"utcdate({instant})", 29, zone);
        AssertNumber($"utcday({instant})", 4, zone);
        AssertNumber($"utchours({instant})", 20, zone);
        AssertNumber($"utcminutes({instant})", 0, zone);
        AssertNumber($"utcdayofyear({instant})", 60, zone);

        // Assert: the instant itself does not depend on the zone
        AssertNumber($"time({instant})", Ms(2024, 2, 29, 20), zone);
    }

    /// <summary>
    ///     Validates <c>timezoneoffset</c>: minutes, positive west of Greenwich, following the daylight saving rule.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_Timezoneoffset_IsMinutesWestOfUtc()
    {
        // Arrange
        var us = Calendar(UsLike());

        // Act / Assert: +05:30 is 330 minutes east, so the offset is -330; UTC is 0 (never negative zero)
        AssertNumber($"timezoneoffset({Anchor})", -330, Calendar(Fixed(5, 30)));
        AssertNumber($"timezoneoffset({Anchor})", 180, Calendar(Fixed(-3)));
        Assert.False(double.IsNegative(EvalIn($"timezoneoffset({Anchor})").Number));

        // Assert: the US-like zone is -5 h in January and -4 h in July (daylight time), the change is at 07:00Z
        AssertNumber($"timezoneoffset({Ms(2024, 1, 15):0})", 300, us);
        AssertNumber($"timezoneoffset({Ms(2024, 7, 15):0})", 240, us);
        AssertNumber($"timezoneoffset({Ms(2024, 3, 10, 6, 59, 59, 999):0})", 300, us);
        AssertNumber($"timezoneoffset({Ms(2024, 3, 10, 7):0})", 240, us);
    }

    /// <summary>
    ///     Validates the quarter, the week number (Sundays since the first of January), the day of the year and the ISO week
    ///     at hand-derived dates.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_WeekQuarterAndDayOfYear_FollowVega()
    {
        // Act / Assert: the quarter of the 12 months
        int[] quarters = [1, 1, 1, 2, 2, 2, 3, 3, 3, 4, 4, 4];
        for (var month = 0; month < 12; month++)
        {
            AssertNumber($"quarter(datetime(2024, {month}, 1))", quarters[month]);
            AssertNumber($"utcquarter(datetime(2024, {month}, 1))", quarters[month]);
        }

        // Assert: 2012-01-01 is a Sunday, so week 1 starts on it; 2013-01-01 is a Tuesday, so week 0 runs to Saturday the 5th
        AssertNumber("week(datetime(2012, 0, 1))", 1);
        AssertNumber("week(datetime(2012, 0, 7))", 1);
        AssertNumber("week(datetime(2012, 0, 8))", 2);
        AssertNumber("week(datetime(2013, 0, 1))", 0);
        AssertNumber("week(datetime(2013, 0, 2))", 0);
        AssertNumber("week(datetime(2013, 0, 5))", 0);
        AssertNumber("week(datetime(2013, 0, 6))", 1);
        AssertNumber("utcweek(datetime(2013, 0, 6))", 1);

        // Assert: 2024-03-05 has the Sundays Jan 7, 14, 21, 28, Feb 4, 11, 18, 25 and Mar 3 before it
        AssertNumber("week(datetime(2024, 2, 5))", 9);

        // Assert: the day of the year (2024 is a leap year)
        AssertNumber("dayofyear(datetime(2024, 2, 5))", 65);
        AssertNumber("dayofyear(datetime(2024, 0, 1))", 1);
        AssertNumber("dayofyear(datetime(2024, 11, 31))", 366);
        AssertNumber("dayofyear(datetime(2023, 11, 31))", 365);
        AssertNumber("utcdayofyear(datetime(2023, 11, 31))", 365);
    }

    /// <summary>
    ///     Validates the ISO week number around the year boundaries, where a day can belong to the previous or next year.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_Isoweek_AroundTheYearBoundaries()
    {
        // Act / Assert: 2020 starts on a Wednesday in a leap year, so it has 53 weeks and 2021-01-03 (Sunday) is its last day
        AssertNumber("isoweek(datetime(2021, 0, 3))", 53);
        AssertNumber("isoweek(datetime(2021, 0, 4))", 1);
        AssertNumber("isoweek(datetime(2020, 11, 31))", 53);

        // Assert: 2024-12-30 is a Monday and belongs to week 1 of 2025
        AssertNumber("isoweek(datetime(2024, 11, 30))", 1);
        AssertNumber("isoweek(datetime(2024, 11, 29))", 52);

        // Assert: 2022-01-01 is a Saturday of week 52 of 2021; 2015 starts on a Thursday and has 53 weeks
        AssertNumber("isoweek(datetime(2022, 0, 1))", 52);
        AssertNumber("isoweek(datetime(2015, 11, 31))", 53);
        AssertNumber("isoweek(datetime(2016, 0, 3))", 53);
        AssertNumber("isoweek(datetime(2016, 0, 4))", 1);
        AssertNumber("isoweek(datetime(2024, 2, 5))", 10);
        AssertNumber("utcisoweek(datetime(2024, 2, 5))", 10);
    }

    /// <summary>
    ///     Validates that the local week functions use the zone and the UTC ones do not: 2024-03-03T23:00Z is a Sunday in UTC
    ///     and Monday 2024-03-04 04:30 at +05:30; 2024-03-31T23:00Z is in the first quarter in UTC and the second at +05:30.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_WeekFunctions_LocalAndUtcFormsDiffer()
    {
        // Arrange
        var zone = Calendar(Fixed(5, 30));
        var sunday = $"{Ms(2024, 3, 3, 23):0}";
        var endOfMarch = $"{Ms(2024, 3, 31, 23):0}";

        // Act / Assert: day of year 64 (Mar 4) against 63 (Mar 3), ISO week 10 against 9
        AssertNumber($"dayofyear({sunday})", 64, zone);
        AssertNumber($"utcdayofyear({sunday})", 63, zone);
        AssertNumber($"isoweek({sunday})", 10, zone);
        AssertNumber($"utcisoweek({sunday})", 9, zone);
        AssertNumber($"quarter({endOfMarch})", 2, zone);
        AssertNumber($"utcquarter({endOfMarch})", 1, zone);
    }

    /// <summary>
    ///     Validates <c>timeOffset</c> and <c>utcOffset</c>: months roll like <c>setMonth</c>, days keep the wall time across a
    ///     daylight saving change, and an unknown unit or an invalid date gives null.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_Offset_MovesByCalendarUnits()
    {
        // Arrange
        var us = Calendar(UsLike());
        var noonBefore = $"{Ms(2024, 3, 9, 17):0}";

        // Act / Assert: Jan 31 + 1 month is Feb 31, which JavaScript's setMonth turns into Mar 2 (2024 is a leap year)
        AssertDate("timeOffset('month', datetime(2024, 0, 31), 1)", Ms(2024, 3, 2));
        AssertDate("utcOffset('month', datetime(2024, 0, 31), 1)", Ms(2024, 3, 2));
        AssertDate("timeOffset('month', datetime(2024, 0, 15), -1)", Ms(2023, 12, 15));
        AssertDate("utcOffset('year', datetime(2024, 1, 29, 12), 1)", Ms(2025, 3, 1, 12));
        AssertDate("utcOffset('hours', datetime(2024, 2, 5, 10), 2)", Ms(2024, 3, 5, 12));
        AssertDate("utcOffset('hours', datetime(2024, 2, 5, 10))", Ms(2024, 3, 5, 11));
        AssertDate("utcOffset('minutes', datetime(2024, 2, 5, 10), 90)", Ms(2024, 3, 5, 11, 30));
        AssertDate("utcOffset('seconds', datetime(2024, 2, 5), -1)", Ms(2024, 3, 4, 23, 59, 59));
        AssertDate("utcOffset('milliseconds', datetime(2024, 2, 5), 1500)", Ms(2024, 3, 5, 0, 0, 1, 500));
        AssertDate("utcOffset('week', datetime(2024, 2, 5), 2)", Ms(2024, 3, 19));

        // Assert: the quarter is every third month, so one step from Feb 15 is the next quarter start month (April)
        AssertDate("utcOffset('quarter', datetime(2024, 1, 15), 1)", Ms(2024, 4, 15));

        // Assert: a fraction rounds down (Math.floor), also below zero
        AssertDate("utcOffset('hours', datetime(2024, 2, 5, 10), 1.9)", Ms(2024, 3, 5, 11));
        AssertDate("utcOffset('hours', datetime(2024, 2, 5, 10), -0.5)", Ms(2024, 3, 5, 9));

        // Assert: noon on 2024-03-09 (17:00Z at -5 h) plus one local day is noon on 03-10 at -4 h (16:00Z), 23 hours later; UTC adds 24 hours
        AssertDate($"timeOffset('day', {noonBefore}, 1)", Ms(2024, 3, 10, 16), us);
        AssertDate($"utcOffset('day', {noonBefore}, 1)", Ms(2024, 3, 10, 17), us);
        AssertDate($"timeOffset('date', {noonBefore}, 1)", Ms(2024, 3, 10, 16), us);
        AssertDate($"timeOffset('hours', {noonBefore}, 24)", Ms(2024, 3, 10, 17), us);
        AssertDate($"timeOffset('day', {Ms(2024, 3, 10, 16):0}, -1)", Ms(2024, 3, 9, 17), us);
    }

    /// <summary>
    ///     Validates that a unit that is not one of the Vega unit names gives null, and that the names are case sensitive.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_Offset_UnknownUnitsAndDates_AreNull()
    {
        // Act / Assert
        AssertNull("timeOffset('fortnight', datetime(2024, 0, 1), 1)");
        AssertNull("timeOffset('MONTH', datetime(2024, 0, 1), 1)");
        AssertNull("timeOffset('utcmonth', datetime(2024, 0, 1), 1)");
        AssertNull("timeOffset('isoweek', datetime(2024, 0, 1), 1)");
        AssertNull("timeOffset(1, datetime(2024, 0, 1), 1)");
        AssertNull("timeOffset(null, datetime(2024, 0, 1), 1)");
        AssertNull("timeOffset('month', 'bogus', 1)");
        AssertNull("timeOffset('month', null, 1)");
        AssertNull("timeOffset('month', datetime(2024, 0, 1), NaN)");
        AssertNull("utcOffset('year', datetime(9999, 0, 1), 1)");
        AssertNull("timeOffset('milliseconds', datetime(2024, 0, 1), 1e300)");
        AssertNull("timeOffset('" + new string('m', 40) + "', datetime(2024, 0, 1), 1)");
    }

    /// <summary>
    ///     Validates the date sequences: from the first boundary at or after the start up to but excluding the stop.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_Sequence_ListsTheBoundaries()
    {
        // Act / Assert: three days (the stop is exclusive), starting at midnight
        AssertDate("utcSequence('day', datetime(2024, 0, 1), datetime(2024, 0, 4))[0]", Ms(2024, 1, 1));
        AssertNumber("length(utcSequence('day', datetime(2024, 0, 1), datetime(2024, 0, 4)))", 3);
        AssertDate("utcSequence('day', datetime(2024, 0, 1), datetime(2024, 0, 4))[2]", Ms(2024, 1, 3));

        // Assert: a start inside a day begins at the next midnight
        AssertNumber("length(utcSequence('day', datetime(2024, 0, 1, 6), datetime(2024, 0, 4)))", 2);
        AssertDate("utcSequence('day', datetime(2024, 0, 1, 6), datetime(2024, 0, 4))[0]", Ms(2024, 1, 2));

        // Assert: steps and the names of the Vega units
        AssertNumber("length(utcSequence('hours', datetime(2024, 0, 1), datetime(2024, 0, 2), 6))", 4);
        AssertDate("utcSequence('hours', datetime(2024, 0, 1), datetime(2024, 0, 2), 6)[3]", Ms(2024, 1, 1, 18));
        AssertNumber("length(utcSequence('date', datetime(2024, 0, 1), datetime(2024, 0, 8), 2))", 4);
        AssertNumber("length(utcSequence('dayofyear', datetime(2024, 0, 1), datetime(2024, 0, 8)))", 7);
        AssertNumber("length(utcSequence('month', datetime(2024, 0, 1), datetime(2025, 0, 1)))", 12);
        AssertNumber("length(utcSequence('quarter', datetime(2024, 0, 1), datetime(2025, 0, 1)))", 4);
        AssertDate("utcSequence('quarter', datetime(2024, 0, 1), datetime(2025, 0, 1))[3]", Ms(2024, 10, 1));
        AssertNumber("length(utcSequence('year', datetime(2020, 0, 1), datetime(2024, 0, 1)))", 4);
        AssertNumber("length(utcSequence('milliseconds', datetime(2024, 0, 1), datetime(2024, 0, 1, 0, 0, 0, 5)))", 5);
        AssertNumber("length(utcSequence('minutes', datetime(2024, 0, 1), datetime(2024, 0, 1, 0, 3), 1.9))", 3);

        // Assert: nothing when the stop is not after the start, the step is below 1, or an argument is not a date
        AssertNumber("length(utcSequence('day', datetime(2024, 0, 4), datetime(2024, 0, 1)))", 0);
        AssertNumber("length(utcSequence('day', datetime(2024, 0, 1), datetime(2024, 0, 4), 0))", 0);
        AssertNumber("length(utcSequence('day', 'bogus', datetime(2024, 0, 4)))", 0);
        AssertNumber("length(utcSequence('day', datetime(2024, 0, 1), null))", 0);
        AssertNull("utcSequence('fortnight', datetime(2024, 0, 1), datetime(2024, 0, 4))");
        Assert.True(EvalIn("isArray(utcSequence('day', datetime(2024, 0, 1), datetime(2024, 0, 4)))").Boolean);
    }

    /// <summary>
    ///     Validates that a local sequence follows the zone across a daylight saving change: 2024-03-09 00:00 to 2024-03-12
    ///     00:00 local is 05:00Z, 05:00Z (Mar 10 is the 23-hour day) and 04:00Z.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_Sequence_LocalDaysFollowTheZone()
    {
        // Arrange
        var us = Calendar(UsLike());
        var start = $"{Ms(2024, 3, 9, 5):0}";
        var stop = $"{Ms(2024, 3, 12, 4):0}";

        // Act / Assert
        AssertNumber($"length(timeSequence('day', {start}, {stop}))", 3, us);
        AssertDate($"timeSequence('day', {start}, {stop})[0]", Ms(2024, 3, 9, 5), us);
        AssertDate($"timeSequence('day', {start}, {stop})[1]", Ms(2024, 3, 10, 5), us);
        AssertDate($"timeSequence('day', {start}, {stop})[2]", Ms(2024, 3, 11, 4), us);

        // Assert: the UTC form ignores the zone
        AssertNumber($"length(utcSequence('day', {start}, {stop}))", 3, us);
        AssertDate($"utcSequence('day', {start}, {stop})[0]", Ms(2024, 3, 10), us);
    }

    /// <summary>
    ///     Validates that a sequence is bounded by <see cref="VegaLiteLimits.MaxScaleDomainSize"/>: exactly the limit is
    ///     allowed, one more is an error that names the limit.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_Sequence_IsBoundedByTheScaleDomainLimit()
    {
        // Arrange
        var limits = new VegaLiteLimits { MaxScaleDomainSize = 10 };
        var context = new ExpressionFunctionContext(limits, CancellationToken.None);
        DataValue[] Days(int count) =>
            [DataValue.FromString("day"), DataValue.FromDate(Ms(2024, 1, 1)), DataValue.FromDate(Ms(2024, 1, 1) + (count * 86_400_000.0))];

        // Act
        var allowed = ExpressionFunctions.Invoke("utcSequence", Days(10), context);
        var exception = Assert.Throws<ExpressionRuntimeException>(() => ExpressionFunctions.Invoke("utcSequence", Days(11), context));
        var evaluated = Assert.Throws<VegaLiteRenderException>(() => EvalIn("utcSequence('day', datetime(2024, 0, 1), datetime(2024, 3, 1))", null, limits));

        // Assert
        Assert.Equal(10, allowed.Elements.Length);
        Assert.Contains("VegaLiteLimits.MaxScaleDomainSize", exception.Message, StringComparison.Ordinal);
        Assert.Contains("the limit of 10", exception.Message, StringComparison.Ordinal);
        Assert.Contains("VegaLiteLimits.MaxScaleDomainSize", evaluated.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a million-element request stops at the scale domain limit long before it builds a million dates.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_Sequence_OfAMillionMilliseconds_IsRejected()
    {
        // Act
        var exception = Assert.Throws<VegaLiteRenderException>(
            () => Eval("utcSequence('milliseconds', 0, 1000000)"));

        // Assert
        Assert.Contains("VegaLiteLimits.MaxScaleDomainSize", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a sequence costs one step for each date, and that the step budget cuts it off without building
    ///     more dates than the budget allows.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_Sequence_ChargesOneStepPerDate()
    {
        // Arrange
        var roomy = new ExpressionFunctionContext(Steps(1_000), CancellationToken.None);
        var tight = new ExpressionFunctionContext(Steps(500), CancellationToken.None);
        DataValue[] Days(int count) =>
            [DataValue.FromString("day"), DataValue.FromDate(Ms(2024, 1, 1)), DataValue.FromDate(Ms(2024, 1, 1) + (count * 86_400_000.0))];

        // Act
        var result = ExpressionFunctions.Invoke("utcSequence", Days(100), roomy);
        var exception = Assert.Throws<ExpressionRuntimeException>(() => ExpressionFunctions.Invoke("utcSequence", Days(800), tight));

        // Assert
        Assert.Equal(100, result.Elements.Length);
        Assert.Equal(100, roomy.Steps);
        Assert.Contains("VegaLiteLimits.MaxEvaluationSteps", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a sequence observes cancellation.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_Sequence_ObservesCancellation()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var context = new ExpressionFunctionContext(null, cancellation.Token);
        DataValue[] arguments = [DataValue.FromString("day"), DataValue.FromDate(Ms(2024, 1, 1)), DataValue.FromDate(Ms(2024, 2, 1))];

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => ExpressionFunctions.Invoke("utcSequence", arguments, context));
    }

    /// <summary>
    ///     Validates <c>timeUnitSpecifier</c> with Vega's own defaults (the Vega-Lite overrides are not applied).
    /// </summary>
    [Fact]
    public void ExpressionFunctions_TimeUnitSpecifier_UsesTheVegaDefaults()
    {
        // Act / Assert: joined defaults, with the longest known combination first
        AssertText("timeUnitSpecifier(['year', 'month'], {})", "%Y-%m");
        AssertText("timeUnitSpecifier(['year', 'month'])", "%Y-%m");
        AssertText("timeUnitSpecifier(['year', 'month', 'date'])", "%Y-%m-%d");
        AssertText("timeUnitSpecifier(['hours', 'minutes'])", "%H:%M");
        AssertText("timeUnitSpecifier(['month', 'date'])", "%b %d");
        AssertText("timeUnitSpecifier(['year', 'quarter'])", "%Y Q%q");
        AssertText("timeUnitSpecifier(['year', 'isoweek'])", "%G W%V");
        AssertText("timeUnitSpecifier(['year', 'hours'])", "%Y %H:00");
        AssertText("timeUnitSpecifier(['seconds', 'milliseconds'])", ":%S.%L");
        AssertText("timeUnitSpecifier('year')", "%Y");
        AssertText("timeUnitSpecifier('milliseconds')", ".%L");
        AssertText("timeUnitSpecifier('week')", "W%U");
        AssertText("timeUnitSpecifier('dayofyear')", "%j");

        // Assert: the units are put in Vega's order first
        AssertText("timeUnitSpecifier(['month', 'year'])", "%Y-%m");

        // Assert: the overrides replace the defaults, also of a combination; only texts count
        AssertText("timeUnitSpecifier(['year', 'month'], {'year-month': '%B %Y'})", "%B %Y");
        AssertText("timeUnitSpecifier(['year', 'month'], {year: 'Y%Y', month: 'M%m'})", "%Y-%m");
        AssertText("timeUnitSpecifier(['year', 'week'], {year: 'Y%Y '})", "Y%Y W%U");
        AssertText("timeUnitSpecifier(['year'], {year: 5})", "%Y");
    }

    /// <summary>
    ///     Validates that units that Vega rejects (unknown, incompatible, repeated, missing) give null instead of an error.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_TimeUnitSpecifier_InvalidUnits_AreNull()
    {
        // Act / Assert
        AssertNull("timeUnitSpecifier([])");
        AssertNull("timeUnitSpecifier()");
        AssertNull("timeUnitSpecifier(null)");
        AssertNull("timeUnitSpecifier(5)");
        AssertNull("timeUnitSpecifier('fortnight')");
        AssertNull("timeUnitSpecifier('Year')");
        AssertNull("timeUnitSpecifier(['year', 5])");
        AssertNull("timeUnitSpecifier(['week', 'dayofyear'])");
        AssertNull("timeUnitSpecifier(['month', 'dayofyear'])");
        AssertNull("timeUnitSpecifier(['week', 'isoweek'])");
        AssertNull("timeUnitSpecifier(['year', 'year'])");
        AssertNull("timeUnitSpecifier(['year', 'quarter', 'month', 'week', 'isoweek', 'date', 'day', 'dayofyear', 'hours', 'minutes', 'seconds', 'milliseconds', 'year'])");
    }

    /// <summary>
    ///     Validates <c>timeFormat</c> and <c>utcFormat</c> with a specifier text, and the text of a null and an invalid value.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_TimeFormat_FormatsWithTheSpecifier()
    {
        // Arrange
        var zone = Calendar(Fixed(5, 30));

        // Act / Assert: 2024-03-05T13:04:09.007Z
        AssertText("timeFormat(datetime(2024, 2, 5, 13, 4, 9, 7), '%Y-%m-%d')", "2024-03-05");
        AssertText("timeFormat(datetime(2024, 2, 5, 13, 4, 9, 7), '%H:%M:%S.%L')", "13:04:09.007");
        AssertText("utcFormat(datetime(2024, 2, 5, 13, 4, 9, 7), '%I:%M %p')", "01:04 PM");
        AssertText($"timeFormat({Anchor}, '%A, %B %d, %Y')", "Thursday, February 29, 2024");
        AssertText($"utcFormat({Anchor}, '%a %b %e %j %U %W %V')", "Thu Feb 29 060 08 09 09");
        AssertText($"timeFormat({Anchor}, 'no directives')", "no directives");
        AssertText($"timeFormat({Anchor}, '%%')", "%");

        // Assert: the local form uses the zone (12:34 UTC is 18:04 at +05:30), the UTC form does not
        AssertText($"timeFormat({Anchor}, '%H:%M')", "18:04", zone);
        AssertText($"utcFormat({Anchor}, '%H:%M')", "12:34", zone);

        // Assert: null is the text null (Vega), everything that is not an instant is Invalid Date, a number text is a number
        AssertText("timeFormat(null, '%Y')", "null");
        AssertText("utcFormat(null, '%Y')", "null");
        AssertText("timeFormat(NaN, '%Y')", "Invalid Date");
        AssertText("timeFormat()", "Invalid Date");
        AssertText("timeFormat('2024-03-05', '%Y')", "Invalid Date");
        AssertText("timeFormat(1e300, '%Y')", "Invalid Date");
        AssertText($"timeFormat('{Anchor}', '%Y')", "2024");
        AssertText("timeFormat(true, '%L')", "001");
    }

    /// <summary>
    ///     Validates the multi-format (an object as the specifier): the label is as coarse as the largest boundary the instant
    ///     is on, and the object's texts replace the defaults.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_TimeFormat_ObjectSpecifier_IsAMultiFormat()
    {
        // Act / Assert: the default labels
        AssertText($"utcFormat({Ms(2024, 3, 5):0}, {{}})", "Tue 05");
        AssertText($"utcFormat({Ms(2024, 3, 3):0}, {{}})", "Mar 03");
        AssertText($"utcFormat({Ms(2024, 3, 1):0}, {{}})", "March");
        AssertText($"utcFormat({Ms(2024, 4, 1):0}, {{}})", "April");
        AssertText($"utcFormat({Ms(2024, 1, 1):0}, {{}})", "2024");
        AssertText($"utcFormat({Ms(2024, 3, 5, 13):0}, {{}})", "01 PM");
        AssertText($"utcFormat({Ms(2024, 3, 5, 13, 45):0}, {{}})", "01:45");
        AssertText($"utcFormat({Ms(2024, 3, 5, 13, 45, 30):0}, {{}})", ":30");
        AssertText($"utcFormat({Ms(2024, 3, 5, 13, 45, 30, 250):0}, {{}})", ".250");
        AssertText($"timeFormat({Ms(2024, 3, 5):0}, {{}})", "Tue 05");

        // Assert: overrides by level; an override that is not a text is ignored
        AssertText($"utcFormat({Ms(2024, 3, 5):0}, {{date: '%d/%m'}})", "05/03");
        AssertText($"utcFormat({Ms(2024, 1, 1):0}, {{year: 'Y%Y'}})", "Y2024");
        AssertText($"utcFormat({Ms(2024, 1, 1):0}, {{year: 5}})", "2024");
        AssertText($"utcFormat({Ms(2024, 3, 5, 13):0}, {{hours: '%H h'}})", "13 h");

        // Assert: the local multi-format works on the wall time of the zone (2024-03-04T18:30Z is Mar 5 00:00 at +05:30)
        AssertText($"timeFormat({Ms(2024, 3, 4, 18, 30):0}, {{}})", "Tue 05", Calendar(Fixed(5, 30)));
        AssertText($"utcFormat({Ms(2024, 3, 4, 18, 30):0}, {{}})", "06:30", Calendar(Fixed(5, 30)));
    }

    /// <summary>
    ///     Validates that the length of a formatted text is bounded by the string limit, that the specifier is too, and that
    ///     the work of a long specifier is charged to the step budget.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_TimeFormat_IsBoundedAndCharged()
    {
        // Arrange
        var limits = new VegaLiteLimits { MaxStringLength = 10 };
        var context = new ExpressionFunctionContext(limits, CancellationToken.None);
        var budget = new ExpressionFunctionContext(Steps(100), CancellationToken.None);
        DataValue[] ok = [DataValue.FromDate(Ms(2024, 3, 5)), DataValue.FromString("%Y-%m-%d")];
        DataValue[] tooLong = [DataValue.FromDate(Ms(2024, 3, 5)), DataValue.FromString("%Y-%m-%d%Y")];
        DataValue[] hugeSpecifier = [DataValue.FromDate(0), DataValue.FromString(new string('x', 10_000))];

        // Act
        var fits = ExpressionFunctions.Invoke("utcFormat", ok, context);
        var exception = Assert.Throws<ExpressionRuntimeException>(() => ExpressionFunctions.Invoke("utcFormat", tooLong, context));
        var specifier = Assert.Throws<ExpressionRuntimeException>(() => ExpressionFunctions.Invoke("timeFormat", hugeSpecifier, context));
        var charged = Assert.Throws<ExpressionRuntimeException>(() => ExpressionFunctions.Invoke("timeFormat", hugeSpecifier, budget));

        // Assert
        Assert.Equal("2024-03-05", fits.Text);
        Assert.Contains("the limit of 10 characters (VegaLiteLimits.MaxStringLength)", exception.Message, StringComparison.Ordinal);
        Assert.Contains("VegaLiteLimits.MaxStringLength", specifier.Message, StringComparison.Ordinal);
        Assert.Contains("VegaLiteLimits.MaxEvaluationSteps", charged.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates <c>timeParse</c> and <c>utcParse</c>: the text must match the specifier completely, a local parse uses the
    ///     zone, and the result is a date or null.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_TimeParse_ReadsTheSpecifier()
    {
        // Arrange
        var zone = Calendar(Fixed(5, 30));

        // Act / Assert
        AssertDate("timeParse('05/03/2024', '%d/%m/%Y')", Ms(2024, 3, 5));
        AssertDate("utcParse('05/03/2024 13:04:09', '%d/%m/%Y %H:%M:%S')", Ms(2024, 3, 5, 13, 4, 9));
        AssertDate("utcParse('2024-03-05T13:04:09.007Z', '%Y-%m-%dT%H:%M:%S.%LZ')", Ms(2024, 3, 5, 13, 4, 9, 7));

        // Assert: with no year the year is 1900; 01:45 PM is 13:45 on 1900-01-01
        AssertDate("timeParse('01:45 PM', '%I:%M %p')", -2_208_939_300_000);

        // Assert: local midnight at +05:30 is 18:30 UTC of the day before, a UTC parse is not moved
        AssertDate("timeParse('05/03/2024', '%d/%m/%Y')", Ms(2024, 3, 4, 18, 30), zone);
        AssertDate("utcParse('05/03/2024', '%d/%m/%Y')", Ms(2024, 3, 5), zone);

        // Assert: anything left over, anything missing and anything that is not text gives null; d3 does not range check the fields, so month 13 rolls over
        AssertNull("timeParse('05/03/2024 x', '%d/%m/%Y')");
        AssertNull("timeParse('2024', '%d/%m/%Y')");
        AssertNull("timeParse('', '%Y')");
        AssertNull("timeParse(null, '%Y')");
        AssertDate("utcParse('13/13/2024', '%d/%m/%Y')", Ms(2025, 1, 13));
        AssertDate("utcParse(2024, '%Y')", Ms(2024, 1, 1));
        Assert.True(EvalIn("isDate(utcParse('2024', '%Y'))").Boolean);
    }

    /// <summary>
    ///     Validates that the work of a parse is charged by the length of the text, and that a long text is not scanned.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_TimeParse_ChargesTheTextLength()
    {
        // Arrange
        var budget = new ExpressionFunctionContext(Steps(100), CancellationToken.None);
        var roomy = new ExpressionFunctionContext(null, CancellationToken.None);
        DataValue[] text = [DataValue.FromString(new string('1', 10_000)), DataValue.FromString("%Y")];

        // Act
        var result = ExpressionFunctions.Invoke("utcParse", text, roomy);
        var exception = Assert.Throws<ExpressionRuntimeException>(() => ExpressionFunctions.Invoke("utcParse", text, budget));

        // Assert
        Assert.True(result.IsNull);
        Assert.Equal(156, roomy.Steps);
        Assert.Contains("VegaLiteLimits.MaxEvaluationSteps", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the name formatters: months and weekdays count from 0 (January, Sunday), other whole numbers roll over
    ///     like the setters of a date, and anything that is not a whole number gives the empty text.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_NameFormatters_GiveTheEnglishNames()
    {
        // Act / Assert
        AssertText("monthFormat(0)", "January");
        AssertText("monthFormat(8)", "September");
        AssertText("monthFormat(11)", "December");
        AssertText("monthFormat(12)", "January");
        AssertText("monthFormat(-1)", "December");
        AssertText("monthAbbrevFormat(0)", "Jan");
        AssertText("monthAbbrevFormat(1)", "Feb");
        AssertText("monthAbbrevFormat(8)", "Sep");
        AssertText("dayFormat(0)", "Sunday");
        AssertText("dayFormat(6)", "Saturday");
        AssertText("dayFormat(7)", "Sunday");
        AssertText("dayFormat(-1)", "Saturday");
        AssertText("dayAbbrevFormat(1)", "Mon");
        AssertText("dayAbbrevFormat(3)", "Wed");

        // Assert: not a whole number
        AssertText("monthFormat(1.5)", string.Empty);
        AssertText("monthFormat('1')", string.Empty);
        AssertText("monthFormat(null)", string.Empty);
        AssertText("monthFormat()", string.Empty);
        AssertText("dayFormat(NaN)", string.Empty);
        AssertText("dayAbbrevFormat(Infinity)", string.Empty);

        // Assert: the zone plays no part
        AssertText("monthFormat(0)", "January", Calendar(Fixed(-12)));
    }

    /// <summary>
    ///     Validates <c>toDate</c>: null and the empty text are missing, a date stays, a number is milliseconds, a text is
    ///     parsed in the calendar, and everything else is null.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_ToDate_ConvertsInstants()
    {
        // Act / Assert
        AssertDate("toDate('2024-03-05')", Ms(2024, 3, 5));
        AssertDate("toDate('2024-03-05T13:04:09.007Z')", Ms(2024, 3, 5, 13, 4, 9, 7));
        AssertDate("toDate('March 5, 2024')", Ms(2024, 3, 5));
        AssertDate($"toDate({Anchor})", AnchorMilliseconds);
        AssertDate("toDate(toDate(0))", 0);
        AssertNull("toDate(null)");
        AssertNull("toDate('')");
        AssertNull("toDate()");
        AssertNull("toDate('bogus')");
        AssertNull("toDate(NaN)");
        AssertNull("toDate(true)");
        AssertNull("toDate([1])");

        // Assert: a date-time without an offset is in the zone of the calendar (10:00 at +05:30 is 04:30Z)
        AssertDate("toDate('2024-03-05T10:00:00')", Ms(2024, 3, 5, 4, 30), Calendar(Fixed(5, 30)));
        AssertDate("toDate('2024-03-05T10:00:00')", Ms(2024, 3, 5, 10));
        Assert.True(EvalIn("isDate(toDate('2024-03-05'))").Boolean);
    }

    /// <summary>
    ///     Validates that a date text is cut off at 128 characters before it is looked at, so a huge text is cheap and no date.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_DateText_IsCutOffAndCheap()
    {
        // Arrange
        var context = new ExpressionFunctionContext(Steps(10), CancellationToken.None);
        DataValue[] huge = [DataValue.FromString(new string('2', 1_000_000))];

        // Act
        var date = ExpressionFunctions.Invoke("toDate", huge, context);
        var year = ExpressionFunctions.Invoke("year", huge, context);

        // Assert: two steps for each of the two calls (128 characters), and no date
        Assert.True(date.IsNull);
        Assert.True(double.IsNaN(year.Number));
        Assert.Equal(4, context.Steps);
    }

    /// <summary>
    ///     Validates the functions against a datum: a date field, a missing field and an invalid field.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_Time_AgainstADatum()
    {
        // Arrange
        var datum = Obj(("d", DataValue.FromDate(AnchorMilliseconds)), ("n", default), ("s", DataValue.FromString("2024-03-05")));

        // Act / Assert
        Assert.Equal(2024, EvalWith("year(datum.d)", datum).Number);
        Assert.Equal("2024-02", EvalWith("utcFormat(datum.d, '%Y-%m')", datum).Text);
        Assert.Equal("null", EvalWith("timeFormat(datum.n, '%Y')", datum).Text);
        Assert.True(double.IsNaN(EvalWith("year(datum.n)", datum).Number));
        Assert.Equal(5, EvalWith("date(datum.s)", datum).Number);
        Assert.False(EvalWith("isValid(toDate(datum.n))", datum).Boolean);
        Assert.Equal(Ms(2024, 3, 5), EvalWith("toDate(datum.s)", datum).Milliseconds);
        Assert.Equal(2024, EvalWith("year(timeOffset('month', datum.d, 1))", datum).Number);
    }

    /// <summary>
    ///     Validates that <c>now</c> is still not a function: the call is reported by the parser and gives null.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_Now_StaysUnsupported()
    {
        // Arrange / Act
        var compiled = Compile("timeFormat(now(), '%Y')");

        // Assert
        Assert.False(ExpressionFunctions.TryGet("now", out _));
        Assert.Equal(["vl-expr-function-now"], compiled.UnsupportedConstructs.Select(c => c.Identifier).ToArray());
        AssertNull("now()");
        AssertText("timeFormat(now(), '%Y')", "null");
    }

    /// <summary>
    ///     Validates that the results do not depend on the culture of the thread: the Turkish dotted and dotless I and the
    ///     German number and date conventions play no part.
    /// </summary>
    /// <param name="culture">The culture name.</param>
    [Theory]
    [InlineData("tr-TR")]
    [InlineData("de-DE")]
    [InlineData("")]
    public void ExpressionFunctions_Time_AreCultureInvariant(string culture)
    {
        // Arrange
        const string Text = "[monthFormat(8), dayFormat(1), dayAbbrevFormat(3), utcFormat(1709643849007, '%I:%M:%S.%L %p %B %A'), "
            + "toString(timeOffset('minutes', 1709643849007, 1)), toString(isoweek(1709643849007)), "
            + "toString(timeParse('05/03/2024', '%d/%m/%Y')), utcFormat(1709643849007, {}), toString(year('2024-03-05')), "
            + "toString(utcSequence('hours', 0, 7200000)[1])]";

        // Act
        var result = UnderCulture(culture, () => Eval("join(" + Text + ", '|')").Text);

        // Assert: 2024-03-05T13:04:09.007Z
        Assert.Equal(
            "September|Monday|Wed|01:04:09.007 PM March Tuesday|2024-03-05T13:05:09.007Z|10|2024-03-05T00:00:00.000Z|.007|2024|1970-01-01T01:00:00.000Z",
            result);
    }

    /// <summary>
    ///     Validates that the same expression gives the same result when it is evaluated again (nothing is carried between
    ///     evaluations, no clock or zone is read).
    /// </summary>
    [Fact]
    public void ExpressionFunctions_Time_AreDeterministic()
    {
        // Arrange
        const string Text = "utcFormat(datetime(2024, 1, 29, 12, 34, 56, 789), '%c') + timeFormat(1709210096789, {}) + toString(time(datetime(2024, 5, 1)))";
        var zone = Calendar(UsLike());

        // Act
        var first = EvalIn(Text, zone);
        var second = EvalIn(Text, zone);

        // Assert: the %c form of the UTC instant 17:34:56 (12:34:56 local), the multi-format of 12:34:56.789 local (a millisecond label) and the instant of 2024-06-01 00:00 local (daylight time, 04:00Z)
        Assert.Equal("2/29/2024, 5:34:56 PM" + ".789" + $"{Ms(2024, 6, 1, 4):0}", first.Text);
        Assert.Equal(first.Text, second.Text);
    }

    /// <summary>
    ///     Validates the context additions of this work package: whole-step charges, the token, the string limit error and the
    ///     cache of compiled time formatters.
    /// </summary>
    [Fact]
    public void ExpressionFunctionContext_TimeSupport_ChargesCachesAndExposesTheToken()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var context = new ExpressionFunctionContext(Steps(10), cancellation.Token);

        // Act
        context.ChargeSteps(0);
        context.ChargeSteps(-5);
        var untouched = context.Steps;
        context.ChargeSteps(10);
        var charged = context.Steps;
        var exhausted = Assert.Throws<ExpressionRuntimeException>(() => context.ChargeSteps(1));
        var first = context.GetTimeFormatter("%Y");
        var second = context.GetTimeFormatter("%Y");

        // Assert
        Assert.Equal(0, untouched);
        Assert.Equal(10, charged);
        Assert.Contains("VegaLiteLimits.MaxEvaluationSteps", exhausted.Message, StringComparison.Ordinal);
        Assert.Equal(cancellation.Token, context.CancellationToken);
        Assert.Same(first, second);
        Assert.Contains("VegaLiteLimits.MaxStringLength", context.StringLimitExceeded().Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the formatter cache clears itself when it is full and keeps giving correct results.
    /// </summary>
    [Fact]
    public void ExpressionFunctionContext_TimeFormatterCache_ClearsWhenFull()
    {
        // Arrange
        var context = new ExpressionFunctionContext(null, CancellationToken.None);
        var first = context.GetTimeFormatter("%Y");

        // Act: 70 distinct specifiers overflow the 64 entries
        for (var i = 0; i < 70; i++)
        {
            context.GetTimeFormatter($"%Y-{i}");
        }

        var again = context.GetTimeFormatter("%Y");

        // Assert: the old entry was dropped and compiled again, and both formatters work
        Assert.NotSame(first, again);
        Assert.Equal("2024", again.Format(AnchorMilliseconds, TimeCalendar.Utc, 100));
    }

    /// <summary>
    ///     Validates the calendar of an evaluator: the local functions read it, a missing calendar means UTC.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_Time_DefaultCalendarIsUtc()
    {
        // Arrange
        var defaultContext = new ExpressionFunctionContext(null, CancellationToken.None);
        var zoned = new ExpressionFunctionContext(null, CancellationToken.None, Calendar(Fixed(2)));
        DataValue[] arguments = [DataValue.FromNumber(AnchorMilliseconds)];

        // Act
        var utcHours = ExpressionFunctions.Invoke("hours", arguments, defaultContext);
        var zonedHours = ExpressionFunctions.Invoke("hours", arguments, zoned);

        // Assert: 12:34 UTC is 14:34 at +02:00
        Assert.Equal(12, utcHours.Number);
        Assert.Equal(14, zonedHours.Number);
    }

    /// <summary>
    ///     Validates that a time format specifier longer than <see cref="TimeFormat.MaxSpecifierLength"/> characters, or one that expands (through <c>%c</c>) to more than
    ///     <see cref="TimeFormat.MaxTokenCount"/> pieces, is a runtime error in every function that reads one, and that a specifier at the limit still works.
    /// </summary>
    [Fact]
    public void ExpressionFunctions_TimeFormat_SpecifierLimits_AreRuntimeErrors()
    {
        // Arrange: the string limit is the default (a million), so only the specifier limits are in play
        var context = new ExpressionFunctionContext(null, CancellationToken.None);
        var longText = new string('x', TimeFormat.MaxSpecifierLength + 1);
        var manyPieces = string.Concat(Enumerable.Repeat("%c", 400));
        DataValue[] Args(string specifier) => [DataValue.FromDate(Ms(2024, 3, 5)), DataValue.FromString(specifier)];
        DataValue[] ParseArgs(string specifier) => [DataValue.FromString("2024"), DataValue.FromString(specifier)];
        var members = DataObject.Create([new KeyValuePair<string, DataValue>("year", DataValue.FromString(longText))]);
        DataValue[] Multi() => [DataValue.FromDate(Ms(2024, 1, 1)), DataValue.FromObject(members)];

        // Act
        var formatLong = Assert.Throws<ExpressionRuntimeException>(() => ExpressionFunctions.Invoke("timeFormat", Args(longText), context));
        var utcLong = Assert.Throws<ExpressionRuntimeException>(() => ExpressionFunctions.Invoke("utcFormat", Args(longText), context));
        var formatPieces = Assert.Throws<ExpressionRuntimeException>(() => ExpressionFunctions.Invoke("utcFormat", Args(manyPieces), context));
        var multiLong = Assert.Throws<ExpressionRuntimeException>(() => ExpressionFunctions.Invoke("timeFormat", Multi(), context));
        var parseLong = Assert.Throws<ExpressionRuntimeException>(() => ExpressionFunctions.Invoke("timeParse", ParseArgs(longText), context));
        var utcParseLong = Assert.Throws<ExpressionRuntimeException>(() => ExpressionFunctions.Invoke("utcParse", ParseArgs(longText), context));
        var atLimit = ExpressionFunctions.Invoke("utcFormat", Args(new string('x', TimeFormat.MaxSpecifierLength)), context);

        // Assert: each error names its limit, and the longest specifier is formatted
        Assert.All([formatLong, utcLong, multiLong, parseLong, utcParseLong], e => Assert.Contains("the limit of 1024 characters", e.Message, StringComparison.Ordinal));
        Assert.Contains("the limit of 4096 pieces", formatPieces.Message, StringComparison.Ordinal);
        Assert.Equal(TimeFormat.MaxSpecifierLength, atLimit.Text!.Length);
    }

    /// <summary>
    ///     Validates that the cache of compiled time formatters is bounded by size: a long specifier is compiled again for every call and never kept, a short one is
    ///     kept, and a failed compilation caches nothing.
    /// </summary>
    [Fact]
    public void ExpressionFunctionContext_TimeFormatterCache_DoesNotKeepLongSpecifiers()
    {
        // Arrange: a specifier of 200 characters is within the limit of the formatter but over the size the cache keeps
        var context = new ExpressionFunctionContext(null, CancellationToken.None);
        var longer = new string('x', 200) + "%Y";

        // Act
        var firstLong = context.GetTimeFormatter(longer);
        var secondLong = context.GetTimeFormatter(longer);
        var firstShort = context.GetTimeFormatter("%Y");
        var secondShort = context.GetTimeFormatter("%Y");
        var failed = Assert.Throws<ExpressionRuntimeException>(() => context.GetTimeFormatter(new string('x', TimeFormat.MaxSpecifierLength + 1)));

        // Assert: both long formatters work but are different objects; the short one is the cached one
        Assert.NotSame(firstLong, secondLong);
        Assert.Equal(longer.Length - 2 + 4, firstLong.Format(AnchorMilliseconds, TimeCalendar.Utc, 1_000).Length);
        Assert.Same(firstShort, secondShort);
        Assert.Contains("the limit of 1024 characters", failed.Message, StringComparison.Ordinal);
    }
}
