using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Tests.Expressions;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Data;

/// <summary>
///     Unit tests of <see cref="TimeUnitSpec"/>: the names and parts of the Vega-Lite time units, the field names, the label
///     specifiers, Vega's floor and bin rules, the end of a bin and the duration of a step. The expected values are derived by
///     hand from the Vega-Lite and Vega definitions (the derivations are in the comments).
/// </summary>
public class TimeUnitSpecTests
{
    /// <summary>The number of milliseconds in a day.</summary>
    private const double Day = 86_400_000;

    /// <summary>The number of milliseconds in a 365-day year, the unit of Vega's bin table.</summary>
    private const double Year = 365 * Day;

    /// <summary>
    ///     Parses a time unit that must be valid.
    /// </summary>
    /// <param name="name">The unit name.</param>
    /// <returns>The unit.</returns>
    private static TimeUnitSpec Parse(string name)
    {
        Assert.True(TimeUnitSpec.TryParse(name, out var spec, out var error), error);
        return spec;
    }

    /// <summary>
    ///     Parses a time unit object that must be valid.
    /// </summary>
    /// <param name="json">The JSON of the object.</param>
    /// <returns>The unit.</returns>
    private static TimeUnitSpec ParseObject(string json)
    {
        Assert.True(TimeUnitSpec.TryParse(TimeTestSupport.Node(json), out var spec, out var error), error);
        return spec;
    }

    /// <summary>
    ///     Validates the parts of all 11 single and 29 multi time units of Vega-Lite, in the order year, quarter, month, week, day, day of year, date, hours, minutes, seconds, milliseconds.
    /// </summary>
    [Theory]
    [InlineData("year", "year")]
    [InlineData("quarter", "quarter")]
    [InlineData("month", "month")]
    [InlineData("week", "week")]
    [InlineData("day", "day")]
    [InlineData("dayofyear", "dayofyear")]
    [InlineData("date", "date")]
    [InlineData("hours", "hours")]
    [InlineData("minutes", "minutes")]
    [InlineData("seconds", "seconds")]
    [InlineData("milliseconds", "milliseconds")]
    [InlineData("yearquarter", "year,quarter")]
    [InlineData("yearquartermonth", "year,quarter,month")]
    [InlineData("yearmonth", "year,month")]
    [InlineData("yearmonthdate", "year,month,date")]
    [InlineData("yearmonthdatehours", "year,month,date,hours")]
    [InlineData("yearmonthdatehoursminutes", "year,month,date,hours,minutes")]
    [InlineData("yearmonthdatehoursminutesseconds", "year,month,date,hours,minutes,seconds")]
    [InlineData("yearweek", "year,week")]
    [InlineData("yearweekday", "year,week,day")]
    [InlineData("yearweekdayhours", "year,week,day,hours")]
    [InlineData("yearweekdayhoursminutes", "year,week,day,hours,minutes")]
    [InlineData("yearweekdayhoursminutesseconds", "year,week,day,hours,minutes,seconds")]
    [InlineData("yeardayofyear", "year,dayofyear")]
    [InlineData("quartermonth", "quarter,month")]
    [InlineData("monthdate", "month,date")]
    [InlineData("monthdatehours", "month,date,hours")]
    [InlineData("monthdatehoursminutes", "month,date,hours,minutes")]
    [InlineData("monthdatehoursminutesseconds", "month,date,hours,minutes,seconds")]
    [InlineData("weekday", "week,day")]
    [InlineData("weekdayhours", "week,day,hours")]
    [InlineData("weekdayhoursminutes", "week,day,hours,minutes")]
    [InlineData("weekdayhoursminutesseconds", "week,day,hours,minutes,seconds")]
    [InlineData("dayhours", "day,hours")]
    [InlineData("dayhoursminutes", "day,hours,minutes")]
    [InlineData("dayhoursminutesseconds", "day,hours,minutes,seconds")]
    [InlineData("hoursminutes", "hours,minutes")]
    [InlineData("hoursminutesseconds", "hours,minutes,seconds")]
    [InlineData("minutesseconds", "minutes,seconds")]
    [InlineData("secondsmilliseconds", "seconds,milliseconds")]
    public void TimeUnitSpec_Parse_AcceptsEveryVegaLiteName(string name, string expectedParts)
    {
        // Arrange / Act: parse the local name and its utc form
        var local = Parse(name);
        var utc = Parse("utc" + name);

        // Assert: the same parts, the utc flag only on the second, and the normalized unit name
        Assert.Equal(expectedParts.Split(','), local.Parts);
        Assert.Equal(expectedParts.Split(','), utc.Parts);
        Assert.False(local.Utc);
        Assert.True(utc.Utc);
        Assert.False(local.IsBinned);
        Assert.Equal(name, local.Unit);
        Assert.Equal(name, utc.Unit);
        Assert.Equal(1.0, local.Step);
        Assert.Null(local.MaxBins);
    }

    /// <summary>
    ///     Validates that the binned units are recognized with their parts and flags, and that only the units with a binned form are.
    /// </summary>
    [Fact]
    public void TimeUnitSpec_Parse_BinnedUnits()
    {
        // Arrange / Act: a binned local and a binned utc unit
        var local = Parse("binnedyearmonth");
        var utc = Parse("binnedutcyearmonthdate");

        // Assert: the flags and parts
        Assert.True(local.IsBinned);
        Assert.False(local.Utc);
        Assert.Equal(["year", "month"], local.Parts);
        Assert.True(utc.IsBinned);
        Assert.True(utc.Utc);
        Assert.Equal(["year", "month", "date"], utc.Parts);
        Assert.False(TimeUnitSpec.TryParse("binnedmonth", out _, out _));
        Assert.False(TimeUnitSpec.TryParse("binnedhours", out _, out _));
    }

    /// <summary>
    ///     Validates that names that are not Vega-Lite time units are rejected with a message that names them.
    /// </summary>
    [Theory]
    [InlineData("foo")]
    [InlineData("")]
    [InlineData("year-week-date")]
    [InlineData("day-month")]
    [InlineData("Month")]
    [InlineData("utc-utc-month")]
    [InlineData("utc")]
    [InlineData("months")]
    public void TimeUnitSpec_Parse_RejectsUnknownAndIncompatible(string name)
    {
        // Arrange / Act: parse the name
        var ok = TimeUnitSpec.TryParse(name, out var spec, out var error);

        // Assert: no unit and an explanation
        Assert.False(ok);
        Assert.Null(spec);
        Assert.Contains($"'{name}'", error, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the object form: unit, step, utc, and the combination of the prefix with the flag.
    /// </summary>
    [Fact]
    public void TimeUnitSpec_Parse_ObjectForm()
    {
        // Arrange / Act: parse a few objects
        var stepped = ParseObject("""{ "unit": "month", "step": 3 }""");
        var flagged = ParseObject("""{ "unit": "yearmonth", "utc": true }""");
        var prefixed = ParseObject("""{ "unit": "utcyearmonth" }""");
        var binned = ParseObject("""{ "unit": "yearmonth", "binned": true }""");
        var bins = ParseObject("""{ "maxbins": 10 }""");

        // Assert: unit, step, utc, binned and maxbins
        Assert.Equal(new[] { "month" }, stepped.Parts);
        Assert.Equal(3.0, stepped.Step);
        Assert.False(stepped.Utc);
        Assert.False(stepped.IsBinned);
        Assert.Equal(new[] { "year", "month" }, flagged.Parts);
        Assert.Equal(1.0, flagged.Step);
        Assert.True(flagged.Utc);
        Assert.True(prefixed.Utc);
        Assert.Equal("yearmonth", prefixed.Unit);
        Assert.True(binned.IsBinned);
        Assert.Empty(bins.Parts);
        Assert.Equal(10.0, bins.MaxBins);
        Assert.Null(bins.Unit);
    }

    /// <summary>
    ///     Validates that invalid objects are rejected: a zero or negative step, bad types, unknown keys, no unit and no maxbins, and a non-object.
    /// </summary>
    [Theory]
    [InlineData("""{ "unit": "month", "step": 0 }""")]
    [InlineData("""{ "unit": "month", "step": -1 }""")]
    [InlineData("""{ "unit": "month", "step": "2" }""")]
    [InlineData("""{ "unit": "month", "utc": "yes" }""")]
    [InlineData("""{ "unit": "month", "binned": 1 }""")]
    [InlineData("""{ "unit": "foo" }""")]
    [InlineData("""{ "unit": 3 }""")]
    [InlineData("""{ "unit": "month", "extra": 1 }""")]
    [InlineData("""{ "step": 2 }""")]
    [InlineData("""{ "maxbins": 0 }""")]
    [InlineData("""{ }""")]
    [InlineData("\"month\"")]
    [InlineData("3")]
    public void TimeUnitSpec_Parse_RejectsInvalidObjects(string json)
    {
        // Arrange / Act: parse the node
        var ok = TimeUnitSpec.TryParse(TimeTestSupport.Node(json), out var spec, out var error);

        // Assert: rejected with a message
        Assert.False(ok);
        Assert.Null(spec);
        Assert.False(string.IsNullOrEmpty(error));
    }

    /// <summary>
    ///     Validates the names of derived fields as Vega-Lite builds them: the unit, then <c>_step_n</c> and the like, with the utc prefix first and the field last.
    /// </summary>
    [Theory]
    [InlineData("\"yearmonth\"", "date", "yearmonth_date")]
    [InlineData("\"utcmonth\"", "x", "utcmonth_x")]
    [InlineData("""{ "unit": "month", "step": 2 }""", "date", "month_step_2_date")]
    [InlineData("""{ "maxbins": 10 }""", "d", "timeunit_maxbins_10_d")]
    [InlineData("""{ "unit": "month", "utc": true }""", "f", "utcmonth_f")]
    [InlineData("""{ "unit": "utcmonth" }""", "f", "utcmonth_f")]
    [InlineData("""{ "step": 2, "unit": "month" }""", "f", "_step_2month_f")]
    [InlineData("""{ "unit": "yearmonth", "step": 2.5 }""", "f", "yearmonth_step_2_5_f")]
    [InlineData("\"binnedyearmonth\"", "f", "yearmonth_binned_true_f")]
    public void TimeUnitSpec_FieldName_MatchesVegaLiteNaming(string json, string field, string expected)
    {
        // Arrange: a unit given as a string or as an object
        var node = TimeTestSupport.Node(json);
        var spec = node.IsString ? Parse(node.StringValue!) : ParseObject(json);

        // Act: name the derived field
        var name = spec.FieldName(field);

        // Assert: Vega-Lite's name
        Assert.Equal(expected, name);
    }

    /// <summary>
    ///     Validates the label specifiers: each is Vega's <c>timeUnitSpecifier</c> of the parts with the Vega-Lite overrides, worked out from the key table of Vega.
    /// </summary>
    [Theory]
    [InlineData("month", "%b")]
    [InlineData("date", "%d")]
    [InlineData("year", "%Y")]
    [InlineData("quarter", "Q%q")]
    [InlineData("day", "%a")]
    [InlineData("week", "W%U")]
    [InlineData("dayofyear", "%j")]
    [InlineData("hours", "%H:00")]
    [InlineData("minutes", "00:%M")]
    [InlineData("seconds", ":%S")]
    [InlineData("milliseconds", ".%L")]
    [InlineData("yearmonth", "%b %Y")]
    [InlineData("yearmonthdate", "%b %d, %Y")]
    [InlineData("monthdate", "%b %d")]
    [InlineData("yearquarter", "%Y Q%q")]
    [InlineData("yearquartermonth", "%Y Q%q %b")]
    [InlineData("quartermonth", "Q%q %b")]
    [InlineData("yearweek", "%Y W%U")]
    [InlineData("yeardayofyear", "%Y %j")]
    [InlineData("weekday", "W%U %a")]
    [InlineData("dayhours", "%a %H:00")]
    [InlineData("hoursminutes", "%H:%M")]
    [InlineData("hoursminutesseconds", "%H:%M:%S")]
    [InlineData("yearmonthdatehours", "%b %d, %Y %H:00")]
    [InlineData("yearmonthdatehoursminutes", "%b %d, %Y %H:%M")]
    [InlineData("minutesseconds", "00:%M:%S")]
    [InlineData("secondsmilliseconds", ":%S.%L")]
    public void TimeUnitSpec_Specifier_FollowsVegaTimeUnitSpecifier(string unit, string expected)
    {
        // Arrange / Act: the specifier of the unit
        var specifier = Parse(unit).Specifier();

        // Assert: the longest matching keys joined and trimmed
        Assert.Equal(expected, specifier);
    }

    /// <summary>
    ///     Validates the floors of units that lack a year or a month: the missing year is 2012 and the missing month January.
    /// </summary>
    [Theory]
    [InlineData("year", 2024, 3, 17, 10, 20, 2024, 1, 1, 0, 0)]
    [InlineData("month", 2024, 3, 17, 10, 20, 2012, 3, 1, 0, 0)]
    [InlineData("date", 2024, 3, 17, 10, 20, 2012, 1, 17, 0, 0)]
    [InlineData("hours", 2024, 3, 17, 10, 20, 2012, 1, 1, 10, 0)]
    [InlineData("minutes", 2024, 3, 17, 10, 20, 2012, 1, 1, 0, 20)]
    [InlineData("yearmonth", 2024, 3, 17, 10, 20, 2024, 3, 1, 0, 0)]
    [InlineData("monthdate", 2024, 3, 17, 10, 20, 2012, 3, 17, 0, 0)]
    [InlineData("hoursminutes", 2024, 3, 17, 10, 20, 2012, 1, 1, 10, 20)]
    [InlineData("yearmonthdatehours", 2024, 3, 17, 10, 20, 2024, 3, 17, 10, 0)]
    public void TimeUnitSpec_Floor_UsesReferenceYear2012_ForUnitsWithoutYear(
        string unit,
        int year,
        int month,
        int day,
        int hour,
        int minute,
        int expectedYear,
        int expectedMonth,
        int expectedDay,
        int expectedHour,
        int expectedMinute)
    {
        // Arrange: the instant and the hand-derived floor
        var instant = TimeTestSupport.Ms(year, month, day, hour, minute, 45, 678);
        var expected = TimeTestSupport.Ms(expectedYear, expectedMonth, expectedDay, expectedHour, expectedMinute);

        // Act: floor in UTC
        var floor = Parse(unit).Floor(TimeCalendar.Utc, instant);

        // Assert: parts above the unit take the reference value, parts below are cleared
        Assert.Equal(expected, floor);
    }

    /// <summary>
    ///     Validates that the floor of the second and millisecond units keeps the part and clears the rest.
    /// </summary>
    [Fact]
    public void TimeUnitSpec_Floor_SecondsAndMilliseconds()
    {
        // Arrange: an instant with seconds and milliseconds
        var instant = TimeTestSupport.Ms(2024, 3, 17, 10, 20, 45, 678);

        // Act / Assert: the seconds unit keeps :45 and the milliseconds unit keeps .678
        Assert.Equal(TimeTestSupport.Ms(2012, 1, 1, 0, 0, 45), Parse("seconds").Floor(TimeCalendar.Utc, instant));
        Assert.Equal(TimeTestSupport.Ms(2012, 1, 1, 0, 0, 0, 678), Parse("milliseconds").Floor(TimeCalendar.Utc, instant));
        Assert.Equal(TimeTestSupport.Ms(2012, 1, 1, 0, 20, 45), Parse("minutesseconds").Floor(TimeCalendar.Utc, instant));
    }

    /// <summary>
    ///     Validates the units of the day of the week: 2012 starts on a Sunday, so Tuesday is January 3, Sunday is January 1 and Saturday is January 7.
    /// </summary>
    [Fact]
    public void TimeUnitSpec_Floor_Day_IsWeekdayInJanuary2012()
    {
        // Arrange: the day unit
        var day = Parse("day");

        // Act / Assert: Tuesday March 5 2024, Sunday March 3 and Saturday March 2
        Assert.Equal(1_325_548_800_000.0, day.Floor(TimeCalendar.Utc, 1_709_596_800_000.0));
        Assert.Equal(TimeTestSupport.Ms(2012, 1, 1), day.Floor(TimeCalendar.Utc, TimeTestSupport.Ms(2024, 3, 3, 15)));
        Assert.Equal(TimeTestSupport.Ms(2012, 1, 7), day.Floor(TimeCalendar.Utc, TimeTestSupport.Ms(2024, 3, 2, 15)));
    }

    /// <summary>
    ///     Validates the week units. March 5 2024 is in week 9 (the Sundays since January 1: 7, 14, 21, 28 and February 4, 11, 18, 25 and March 3).
    /// </summary>
    [Fact]
    public void TimeUnitSpec_Floor_Week_And_WeekDay()
    {
        // Arrange: the week and the week-day units and Tuesday March 5 2024
        var instant = TimeTestSupport.Ms(2024, 3, 5, 12);

        // Act / Assert: week 9 of 2012 starts on day 63 - 6 = 57 of January, which is Sunday February 26
        Assert.Equal(TimeTestSupport.Ms(2012, 2, 26), Parse("week").Floor(TimeCalendar.Utc, instant));

        // Assert: Tuesday of week 9 is day 2 + 63 - 6 = 59, which is February 28 2012
        Assert.Equal(TimeTestSupport.Ms(2012, 2, 28), Parse("weekday").Floor(TimeCalendar.Utc, instant));

        // Assert: with the year, week 9 of 2024 (January 1 is a Monday): Tuesday is day 2 + 63 - 0 = 65, which is March 5 itself
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 5), Parse("yearweekday").Floor(TimeCalendar.Utc, instant));
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 3), Parse("yearweek").Floor(TimeCalendar.Utc, instant));
    }

    /// <summary>
    ///     Validates the week number rules at the start of the year: January 1 2012 (a Sunday) is week 1, January 7 is week 1, January 8 is week 2, January 1 2013 (a Tuesday) is week 0 and January 6 2013 is week 1; the floor of week 0 of 2013 is December 30 2012.
    /// </summary>
    [Theory]
    [InlineData(2012, 1, 1, 2012, 1, 1)]
    [InlineData(2012, 1, 7, 2012, 1, 1)]
    [InlineData(2012, 1, 8, 2012, 1, 8)]
    [InlineData(2013, 1, 1, 2012, 12, 30)]
    [InlineData(2013, 1, 2, 2012, 12, 30)]
    [InlineData(2013, 1, 5, 2012, 12, 30)]
    [InlineData(2013, 1, 6, 2013, 1, 6)]
    public void TimeUnitSpec_Floor_YearWeek_Handles2013Week0(int year, int month, int day, int expectedYear, int expectedMonth, int expectedDay)
    {
        // Arrange: the year-week unit; week 0 of 2013 gives weekday(0, 0, 2) = 0 - (2 + 6) % 7 = -1, the date before December 31
        var unit = Parse("yearweek");

        // Act: floor the day at 10:00
        var floor = unit.Floor(TimeCalendar.Utc, TimeTestSupport.Ms(year, month, day, 10));

        // Assert: the Sunday that starts the week
        Assert.Equal(TimeTestSupport.Ms(expectedYear, expectedMonth, expectedDay), floor);
    }

    /// <summary>
    ///     Validates the quarter and day-of-year units: the quarter is three times the quarter index, and the day of the year (60 for March 1 2023) is mapped into the leap reference year 2012, where day 60 is February 29.
    /// </summary>
    [Fact]
    public void TimeUnitSpec_Floor_Quarter_And_DayOfYear()
    {
        // Arrange: May 20 2024 is in the second quarter; March 1 2023 is day 60 of its year
        var quarterInstant = TimeTestSupport.Ms(2024, 5, 20);
        var dayInstant = TimeTestSupport.Ms(2023, 3, 1, 5);

        // Act / Assert: quarters
        Assert.Equal(TimeTestSupport.Ms(2012, 4, 1), Parse("quarter").Floor(TimeCalendar.Utc, quarterInstant));
        Assert.Equal(TimeTestSupport.Ms(2024, 4, 1), Parse("yearquarter").Floor(TimeCalendar.Utc, quarterInstant));

        // Assert: days of the year
        Assert.Equal(TimeTestSupport.Ms(2012, 2, 29), Parse("dayofyear").Floor(TimeCalendar.Utc, dayInstant));
        Assert.Equal(TimeTestSupport.Ms(2023, 3, 1), Parse("yeardayofyear").Floor(TimeCalendar.Utc, dayInstant));
    }

    /// <summary>
    ///     Validates that the step applies to the last part only and counts from the first value of the part (from 1 for the date).
    /// </summary>
    [Theory]
    [InlineData("""{ "unit": "month", "step": 3 }""", 2024, 3, 17, 2012, 1, 1, 0)]
    [InlineData("""{ "unit": "month", "step": 3 }""", 2024, 4, 5, 2012, 4, 1, 0)]
    [InlineData("""{ "unit": "yearmonth", "step": 3 }""", 2024, 5, 17, 2024, 4, 1, 0)]
    [InlineData("""{ "unit": "monthdate", "step": 7 }""", 2024, 3, 20, 2012, 3, 15, 0)]
    [InlineData("""{ "unit": "monthdate", "step": 7 }""", 2024, 3, 22, 2012, 3, 22, 0)]
    [InlineData("""{ "unit": "yearmonthdatehours", "step": 6 }""", 2024, 3, 5, 2024, 3, 5, 12)]
    [InlineData("""{ "unit": "year", "step": 10 }""", 2024, 7, 4, 2020, 1, 1, 0)]
    [InlineData("""{ "unit": "month", "step": 1 }""", 2024, 7, 4, 2012, 7, 1, 0)]
    public void TimeUnitSpec_Floor_StepAppliesToLastUnitOnly(string json, int year, int month, int day, int expectedYear, int expectedMonth, int expectedDay, int expectedHour)
    {
        // Arrange: the unit with a step and the instant (13:04 on the day)
        var unit = ParseObject(json);
        var instant = TimeTestSupport.Ms(year, month, day, 13, 4);

        // Act: floor in UTC
        var floor = unit.Floor(TimeCalendar.Utc, instant);

        // Assert: the stepped part is floored to a multiple, the others are plain
        Assert.Equal(TimeTestSupport.Ms(expectedYear, expectedMonth, expectedDay, expectedHour), floor);
    }

    /// <summary>
    ///     Validates the end of a bin: one step of the smallest part after the start (March 2024 ends on April 1; a decade ends ten years later; a quarter is three months).
    /// </summary>
    [Fact]
    public void TimeUnitSpec_End_IsOneStepOfTheSmallestUnit()
    {
        // Arrange / Act / Assert: year-month, the plan's anchors in milliseconds
        Assert.Equal(1_711_929_600_000.0, Parse("yearmonth").End(TimeCalendar.Utc, 1_709_251_200_000.0));
        Assert.Equal(1_333_238_400_000.0, Parse("month").End(TimeCalendar.Utc, 1_330_560_000_000.0));
        Assert.Equal(1_325_548_800_000.0 + Day, Parse("day").End(TimeCalendar.Utc, 1_325_548_800_000.0));

        // Assert: the other parts
        Assert.Equal(TimeTestSupport.Ms(2024, 7, 1), Parse("yearquarter").End(TimeCalendar.Utc, TimeTestSupport.Ms(2024, 4, 1)));
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 10), Parse("yearweek").End(TimeCalendar.Utc, TimeTestSupport.Ms(2024, 3, 3)));
        Assert.Equal(TimeTestSupport.Ms(2025, 1, 1), Parse("year").End(TimeCalendar.Utc, TimeTestSupport.Ms(2024, 1, 1)));
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 5, 14), Parse("yearmonthdatehours").End(TimeCalendar.Utc, TimeTestSupport.Ms(2024, 3, 5, 13)));
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 5, 13, 1), Parse("hoursminutes").End(TimeCalendar.Utc, TimeTestSupport.Ms(2024, 3, 5, 13)));
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 5, 13, 0, 1), Parse("hoursminutesseconds").End(TimeCalendar.Utc, TimeTestSupport.Ms(2024, 3, 5, 13)));
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 5, 13, 0, 0, 1), Parse("secondsmilliseconds").End(TimeCalendar.Utc, TimeTestSupport.Ms(2024, 3, 5, 13)));

        // Assert: steps multiply the part
        Assert.Equal(TimeTestSupport.Ms(2012, 4, 1), ParseObject("""{ "unit": "month", "step": 3 }""").End(TimeCalendar.Utc, TimeTestSupport.Ms(2012, 1, 1)));
        Assert.Equal(TimeTestSupport.Ms(2030, 1, 1), ParseObject("""{ "unit": "year", "step": 10 }""").End(TimeCalendar.Utc, TimeTestSupport.Ms(2020, 1, 1)));
    }

    /// <summary>
    ///     Validates that the end of a day bin follows the civil day in a zone: March 10 starts at 05:00Z and ends at 04:00Z, 23 hours later.
    /// </summary>
    [Fact]
    public void TimeUnitSpec_End_DayFollowsTheCivilDayInAZone()
    {
        // Arrange: a United States like zone and the date unit
        var calendar = TimeTestSupport.Calendar(TimeTestSupport.UsLike());
        var unit = Parse("yearmonthdate");

        // Act: the bin of noon on March 10 2024
        var start = unit.Floor(calendar, TimeTestSupport.Ms(2024, 3, 10, 17));
        var end = unit.End(calendar, start);

        // Assert: 23 hours long
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 10, 5), start);
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 11, 4), end);
    }

    /// <summary>
    ///     Validates that a utc unit ignores the zone of the calendar while a local unit uses it.
    /// </summary>
    [Fact]
    public void TimeUnitSpec_Utc_Units_IgnoreTheZone()
    {
        // Arrange: a zone at minus five hours and an instant at 03:00Z, which is 22:00 of the previous day there
        var calendar = TimeTestSupport.Calendar(TimeTestSupport.UsLike());
        var instant = TimeTestSupport.Ms(2024, 3, 10, 3);

        // Act: floor with the local and with the utc unit
        var local = Parse("yearmonthdate").Floor(calendar, instant);
        var utc = Parse("utcyearmonthdate").Floor(calendar, instant);

        // Assert: local midnight of March 9 is 05:00Z, the utc day is March 10
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 9, 5), local);
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 10), utc);
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 11), Parse("utcyearmonthdate").End(calendar, utc));
    }

    /// <summary>
    ///     Validates that an invalid instant and a unit without parts give NaN instead of throwing.
    /// </summary>
    [Fact]
    public void TimeUnitSpec_Floor_And_End_AreTotal()
    {
        // Arrange: a unit, and the maxbins form that has no parts until it is resolved
        var unit = Parse("yearmonth");
        var unresolved = ParseObject("""{ "maxbins": 10 }""");

        // Act / Assert: NaN and out of range instants
        Assert.True(double.IsNaN(unit.Floor(TimeCalendar.Utc, double.NaN)));
        Assert.True(double.IsNaN(unit.Floor(TimeCalendar.Utc, 1e300)));
        Assert.True(double.IsNaN(unit.End(TimeCalendar.Utc, double.NaN)));
        Assert.True(double.IsNaN(unit.End(TimeCalendar.Utc, DataValue.MaxDateMilliseconds)));

        // Assert: an unresolved unit has no floor, no end, no specifier and no duration
        Assert.True(double.IsNaN(unresolved.Floor(TimeCalendar.Utc, 0)));
        Assert.True(double.IsNaN(unresolved.End(TimeCalendar.Utc, 0)));
        Assert.Null(unresolved.Specifier());
        Assert.Null(unresolved.DurationMilliseconds());
    }

    /// <summary>
    ///     Validates the unit and step that Vega's <c>timeBin</c> picks for an extent: five days in five bins is a day, a year in ten bins is a month, a hundred years in ten bins is ten years, a second in ten bins is 100 milliseconds.
    /// </summary>
    [Fact]
    public void TimeUnitSpec_MaxBins_PicksVegaTimeBinIntervals()
    {
        // Arrange: the maxbins form for several maxbins values
        // Act: resolve for the hand-derived extents
        //  5 days / 5 = 1 day, which is exactly the day entry
        var days = ParseObject("""{ "maxbins": 5 }""").ResolveMaxBins(0, 5 * Day);

        //  365 days / 10 = 36.5 days; 36.5 / 30 = 1.22 is nearer than 90 / 36.5 = 2.47, so the month wins
        var months = ParseObject("""{ "maxbins": 10 }""").ResolveMaxBins(0, Year);

        //  100 years / 10 = a year or more, so whole years with tickStep(0, 100, 10) = 10
        var decades = ParseObject("""{ "maxbins": 10 }""").ResolveMaxBins(0, 100 * Year);

        //  1000 ms / 10 = 100 ms is below a second, so milliseconds with tickStep(0, 1000, 10) = 100
        var millis = ParseObject("""{ "maxbins": 10 }""").ResolveMaxBins(0, 1000);

        // Assert: parts and steps
        Assert.Equal(["year", "month", "date"], days.Parts);
        Assert.Equal(1.0, days.Step);
        Assert.Equal(["year", "month"], months.Parts);
        Assert.Equal(1.0, months.Step);
        Assert.Equal(["year"], decades.Parts);
        Assert.Equal(10.0, decades.Step);
        Assert.Equal(["year", "month", "date", "hours", "minutes", "seconds", "milliseconds"], millis.Parts);
        Assert.Equal(100.0, millis.Step);
    }

    /// <summary>
    ///     Validates that a resolved maxbins unit can floor and name fields, keeps its flags, and that resolving a unit that has parts changes nothing.
    /// </summary>
    [Fact]
    public void TimeUnitSpec_MaxBins_ResolvedUnitFloorsAndKeepsItsName()
    {
        // Arrange: the maxbins form with the utc flag
        var unresolved = ParseObject("""{ "maxbins": 5, "utc": true }""");

        // Act: resolve for five days and floor
        var resolved = unresolved.ResolveMaxBins(0, 5 * Day);
        var floor = resolved.Floor(TimeCalendar.Utc, TimeTestSupport.Ms(2024, 3, 5, 13));

        // Assert: day bins in UTC, the same field name, and no change for a unit that has parts
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 5), floor);
        Assert.True(resolved.Utc);
        Assert.Equal(5.0, resolved.MaxBins);
        Assert.Equal(unresolved.FieldName("f"), resolved.FieldName("f"));
        Assert.Equal("utctimeunit_maxbins_5_f", resolved.FieldName("f"));
        Assert.Same(resolved, resolved.ResolveMaxBins(0, 100 * Year));
        Assert.Equal("%b %d, %Y", resolved.Specifier());
    }

    /// <summary>
    ///     Validates that a non-finite extent resolves to one-year bins instead of NaN steps.
    /// </summary>
    [Fact]
    public void TimeUnitSpec_MaxBins_NonFiniteExtentGivesYears()
    {
        // Arrange / Act: resolve for an empty extent
        var resolved = ParseObject("""{ "maxbins": 10 }""").ResolveMaxBins(double.NaN, double.NaN);

        // Assert: one year
        Assert.Equal(["year"], resolved.Parts);
        Assert.Equal(1.0, resolved.Step);
    }

    /// <summary>
    ///     Validates the duration of one step of the smallest part, measured from 2001-01-01: a month is 31 days, a year 365 days, a quarter 90 days, and the day of the week has none.
    /// </summary>
    [Theory]
    [InlineData("\"month\"", 2_678_400_000.0)]
    [InlineData("\"yearmonth\"", 2_678_400_000.0)]
    [InlineData("\"year\"", 31_536_000_000.0)]
    [InlineData("\"hours\"", 3_600_000.0)]
    [InlineData("\"yearquarter\"", 7_776_000_000.0)]
    [InlineData("\"yearweek\"", 604_800_000.0)]
    [InlineData("\"yearmonthdate\"", 86_400_000.0)]
    [InlineData("\"yeardayofyear\"", 86_400_000.0)]
    [InlineData("\"minutes\"", 60_000.0)]
    [InlineData("\"seconds\"", 1_000.0)]
    [InlineData("\"milliseconds\"", 1.0)]
    [InlineData("""{ "unit": "month", "step": 2 }""", 5_097_600_000.0)]
    [InlineData("""{ "unit": "hours", "step": 6 }""", 21_600_000.0)]
    public void TimeUnitSpec_Duration_IsTheSmallestPartOfTwoThousandOne(string json, double expected)
    {
        // Arrange: the unit
        var node = TimeTestSupport.Node(json);
        var spec = node.IsString ? Parse(node.StringValue!) : ParseObject(json);

        // Act / Assert: the duration
        Assert.Equal(expected, spec.DurationMilliseconds());
    }

    /// <summary>
    ///     Validates that units whose smallest part is the day of the week have no duration.
    /// </summary>
    [Theory]
    [InlineData("day")]
    [InlineData("weekday")]
    [InlineData("yearweekday")]
    public void TimeUnitSpec_Duration_DayOfWeekHasNone(string unit)
    {
        // Arrange / Act / Assert: no minimum step for the weekday
        Assert.Null(Parse(unit).DurationMilliseconds());
    }

    /// <summary>
    ///     Validates which units stand for a single local part, the kind a bare number can be compared with.
    /// </summary>
    [Fact]
    public void TimeUnitSpec_IsSingleLocalUnit_OnlyForOneLocalPart()
    {
        // Arrange / Act / Assert: single local, utc, multi and binned
        Assert.True(Parse("month").IsSingleLocalUnit);
        Assert.False(Parse("utcmonth").IsSingleLocalUnit);
        Assert.False(Parse("yearmonth").IsSingleLocalUnit);
        Assert.False(Parse("binnedyear").IsSingleLocalUnit);
    }

    /// <summary>
    ///     Validates that names, specifiers and floors do not depend on the culture, in particular the Turkish one with its dotless i.
    /// </summary>
    [Fact]
    public void TimeUnitSpec_Parse_Specifier_And_Floor_AreCultureIndependent()
    {
        // Arrange: a summary of units whose names contain the letter i
        static string Summarize()
        {
            var parts = new List<string>();
            foreach (var name in new[] { "minutes", "hoursminutesseconds", "milliseconds", "yearmonthdatehoursminutes", "utcdayofyear" })
            {
                var ok = TimeUnitSpec.TryParse(name, out var spec, out _);
                parts.Add($"{ok}:{spec!.Specifier()}:{spec.FieldName("f")}:{spec.Floor(TimeCalendar.Utc, TimeTestSupport.Ms(2024, 3, 5, 13, 4, 9, 7))}");
            }

            return string.Join("|", parts);
        }

        // Act: run under the three cultures
        var invariant = ExpressionTestSupport.UnderCulture(string.Empty, Summarize);
        var turkish = ExpressionTestSupport.UnderCulture("tr-TR", Summarize);
        var german = ExpressionTestSupport.UnderCulture("de-DE", Summarize);

        // Assert: identical, and the units were accepted
        Assert.StartsWith("True:00:%M:minutes_f:", invariant, StringComparison.Ordinal);
        Assert.Equal(invariant, turkish);
        Assert.Equal(invariant, german);
    }

    /// <summary>
    ///     Validates the reader of a <c>timeUnit</c> value of a specification: a name or an object (a member that a time unit does not define is skipped, which the
    ///     specification parser warned about), nothing for null and for other kinds of value, and the name that titles and field names are made from.
    /// </summary>
    [Fact]
    public void TimeUnitSpec_TryRead_AcceptsNamesAndObjectsAndSkipsUnknownMembers()
    {
        // Act
        var named = TimeUnitSpec.TryRead(TimeTestSupport.Node("\"utcyearmonth\""), out var name);
        var described = TimeUnitSpec.TryRead(TimeTestSupport.Node("""{ "unit": "month", "step": 3, "bogus": 1 }"""), out var obj);
        var strict = TimeUnitSpec.TryParse(TimeTestSupport.Node("""{ "unit": "month", "bogus": 1 }"""), out _, out var error);
        var lenient = TimeUnitSpec.TryParse(TimeTestSupport.Node("""{ "unit": "month", "bogus": 1 }"""), out var kept, out _, ignoreUnknown: true);

        // Assert
        Assert.True(named);
        Assert.True(name!.Utc);
        Assert.Equal("utcyearmonth", name.Name);
        Assert.True(described);
        Assert.Equal(3, obj!.Step);
        Assert.Equal("month_step_3", obj.Name);
        Assert.False(strict);
        Assert.Contains("bogus", error, StringComparison.Ordinal);
        Assert.True(lenient);
        Assert.Equal(["month"], kept!.Parts);
        Assert.False(TimeUnitSpec.TryRead(TimeTestSupport.Node("null"), out _));
        Assert.False(TimeUnitSpec.TryRead(TimeTestSupport.Node("3"), out _));
        Assert.False(TimeUnitSpec.TryRead(TimeTestSupport.Node("\"foo\""), out _));
        Assert.False(TimeUnitSpec.TryRead(TimeTestSupport.Node("""{ "step": 2 }"""), out _));
        Assert.Throws<ArgumentNullException>(() => TimeUnitSpec.TryRead(null!, out _));
    }
}
