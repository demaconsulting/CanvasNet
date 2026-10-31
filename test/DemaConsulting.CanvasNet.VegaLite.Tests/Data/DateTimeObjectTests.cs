using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Tests.Expressions;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Data;

/// <summary>
///     Unit tests of <see cref="DateTimeObject"/>: the keys, names and defaults of a Vega-Lite datetime object, its evaluation
///     in a zone or in UTC, the errors that name the member, and the rule for bare numbers and strings compared with a time unit.
///     Expected instants come from the base class library.
/// </summary>
public class DateTimeObjectTests
{
    /// <summary>
    ///     Reads a datetime object that must be valid.
    /// </summary>
    /// <param name="json">The JSON of the object.</param>
    /// <returns>The object.</returns>
    private static DateTimeObject Read(string json)
    {
        Assert.True(DateTimeObject.TryRead(TimeTestSupport.Node(json), out var value, out var error, out _), error);
        return value;
    }

    /// <summary>
    ///     Parses a time unit that must be valid.
    /// </summary>
    /// <param name="name">The unit name.</param>
    /// <returns>The unit.</returns>
    private static TimeUnitSpec Unit(string name)
    {
        Assert.True(TimeUnitSpec.TryParse(name, out var spec, out var error), error);
        return spec;
    }

    /// <summary>
    ///     Validates the ways to write a month: a number from 1, numeric text, the full name and the three-letter name in any case.
    /// </summary>
    [Theory]
    [InlineData("3")]
    [InlineData("\"3\"")]
    [InlineData("\"March\"")]
    [InlineData("\"mar\"")]
    [InlineData("\"MAR\"")]
    [InlineData("\"march\"")]
    public void DateTimeObject_ToMilliseconds_MonthNamesAndNumbers(string month)
    {
        // Arrange: an object with only a month
        var value = Read($$"""{ "month": {{month}} }""");

        // Act: evaluate in UTC
        var ms = value.ToMilliseconds(TimeCalendar.Utc);

        // Assert: March of the reference year 2012
        Assert.Equal(TimeTestSupport.Ms(2012, 3, 1), ms);
    }

    /// <summary>
    ///     Validates the quarter: it stands for its first month, the month wins when both are given, and a quarter above 4 is flagged and rolls over.
    /// </summary>
    [Fact]
    public void DateTimeObject_ToMilliseconds_Quarter()
    {
        // Arrange / Act / Assert: quarter 1 is January, 3 is July, 4 is October
        Assert.Equal(TimeTestSupport.Ms(2012, 1, 1), Read("""{ "quarter": 1 }""").ToMilliseconds(TimeCalendar.Utc));
        Assert.Equal(TimeTestSupport.Ms(2012, 7, 1), Read("""{ "quarter": 3 }""").ToMilliseconds(TimeCalendar.Utc));
        Assert.Equal(TimeTestSupport.Ms(2012, 10, 1), Read("""{ "quarter": "4" }""").ToMilliseconds(TimeCalendar.Utc));

        // Assert: the month wins over the quarter
        var both = Read("""{ "quarter": 3, "month": 2 }""");
        Assert.Equal(TimeTestSupport.Ms(2012, 2, 1), both.ToMilliseconds(TimeCalendar.Utc));
        Assert.False(both.QuarterOutOfRange);

        // Assert: quarter 5 is month index 12, January of the next year, and is flagged
        var five = Read("""{ "quarter": 5 }""");
        Assert.True(five.QuarterOutOfRange);
        Assert.Equal(TimeTestSupport.Ms(2013, 1, 1), five.ToMilliseconds(TimeCalendar.Utc));
    }

    /// <summary>
    ///     Validates the day of the week: it is a date in the first week of 2012, which starts on a Sunday, and Sunday is 0 or 7.
    /// </summary>
    [Theory]
    [InlineData("\"Sunday\"", 1)]
    [InlineData("0", 1)]
    [InlineData("7", 1)]
    [InlineData("\"sun\"", 1)]
    [InlineData("\"Monday\"", 2)]
    [InlineData("1", 2)]
    [InlineData("\"mon\"", 2)]
    [InlineData("\"Wednesday\"", 4)]
    [InlineData("\"Saturday\"", 7)]
    [InlineData("6", 7)]
    public void DateTimeObject_ToMilliseconds_DayOfWeek(string day, int expectedDate)
    {
        // Arrange: an object with only a day
        var value = Read($$"""{ "day": {{day}} }""");

        // Act: evaluate in UTC
        var ms = value.ToMilliseconds(TimeCalendar.Utc);

        // Assert: that day of the first week of January 2012, and the day is kept
        Assert.Equal(TimeTestSupport.Ms(2012, 1, expectedDate), ms);
        Assert.False(value.DayDropped);
    }

    /// <summary>
    ///     Validates that a day is dropped when the object has any other key (the utc flag counts, as in Vega-Lite) and is reported.
    /// </summary>
    [Fact]
    public void DateTimeObject_TryRead_DayCombinedWithOtherKeysIsDropped()
    {
        // Arrange / Act: a day with a month and a day with the utc flag
        var withMonth = Read("""{ "day": "Monday", "month": 3 }""");
        var withUtc = Read("""{ "day": "Monday", "utc": true }""");

        // Assert: the day is dropped, so the date stays 1
        Assert.True(withMonth.DayDropped);
        Assert.Equal(TimeTestSupport.Ms(2012, 3, 1), withMonth.ToMilliseconds(TimeCalendar.Utc));
        Assert.True(withUtc.DayDropped);
        Assert.Equal(TimeTestSupport.Ms(2012, 1, 1), withUtc.ToMilliseconds(TimeCalendar.Utc));
    }

    /// <summary>
    ///     Validates the utc flag: without it the parts are read in the zone of the chart, with it in UTC.
    /// </summary>
    [Fact]
    public void DateTimeObject_ToMilliseconds_UtcFlagSelectsUtc()
    {
        // Arrange: a zone at minus five hours (January is standard time) and two objects for 05:00
        var calendar = TimeTestSupport.Calendar(TimeTestSupport.UsLike());
        var local = Read("""{ "hours": 5 }""");
        var utc = Read("""{ "hours": 5, "utc": true }""");

        // Act / Assert: local 05:00 is 10:00Z, utc 05:00 is 05:00Z
        Assert.Equal(TimeTestSupport.Ms(2012, 1, 1, 10), local.ToMilliseconds(calendar));
        Assert.Equal(TimeTestSupport.Ms(2012, 1, 1, 5), utc.ToMilliseconds(calendar));
        Assert.True(utc.Utc);
        Assert.False(local.Utc);
    }

    /// <summary>
    ///     Validates the defaults: year 2012, January, the first, midnight.
    /// </summary>
    [Fact]
    public void DateTimeObject_ToMilliseconds_Defaults()
    {
        // Arrange / Act / Assert: an object with one time part and one with only the year
        Assert.Equal(TimeTestSupport.Ms(2012, 1, 1), Read("""{ "hours": 0 }""").ToMilliseconds(TimeCalendar.Utc));
        Assert.Equal(TimeTestSupport.Ms(2020, 1, 1), Read("""{ "year": 2020 }""").ToMilliseconds(TimeCalendar.Utc));
        Assert.Equal(TimeTestSupport.Ms(2012, 1, 1, 0, 0, 0, 5), Read("""{ "milliseconds": 5 }""").ToMilliseconds(TimeCalendar.Utc));
    }

    /// <summary>
    ///     Validates that all keys together give the anchor instant, and that parts that are too large roll over.
    /// </summary>
    [Fact]
    public void DateTimeObject_ToMilliseconds_AllKeysAndOverflow()
    {
        // Arrange / Act / Assert: February 29 2024 12:34:56.789 is the anchor
        var full = Read("""{ "year": 2024, "month": "Feb", "date": 29, "hours": 12, "minutes": 34, "seconds": 56, "milliseconds": 789 }""");
        Assert.Equal(TimeTestSupport.AnchorMilliseconds, full.ToMilliseconds(TimeCalendar.Utc));

        // Assert: date 32 of January is February 1, month 13 is January of the next year, hour 25 is 01:00 the next day, month 0 is December of the year before
        Assert.Equal(TimeTestSupport.Ms(2012, 2, 1), Read("""{ "date": 32 }""").ToMilliseconds(TimeCalendar.Utc));
        Assert.Equal(TimeTestSupport.Ms(2013, 1, 1), Read("""{ "month": 13 }""").ToMilliseconds(TimeCalendar.Utc));
        Assert.Equal(TimeTestSupport.Ms(2012, 1, 2, 1), Read("""{ "hours": 25 }""").ToMilliseconds(TimeCalendar.Utc));
        Assert.Equal(TimeTestSupport.Ms(2011, 12, 1), Read("""{ "month": 0 }""").ToMilliseconds(TimeCalendar.Utc));
    }

    /// <summary>
    ///     Validates that years 0 to 99 mean 1900 to 1999, like the JavaScript date constructor, and that a year outside the range gives NaN.
    /// </summary>
    [Fact]
    public void DateTimeObject_ToMilliseconds_TwoDigitYearsAndOutOfRange()
    {
        // Arrange / Act / Assert: year 24 is 1924
        Assert.Equal(TimeTestSupport.Ms(1924, 1, 1), Read("""{ "year": 24 }""").ToMilliseconds(TimeCalendar.Utc));
        Assert.Equal(TimeTestSupport.Ms(1900, 1, 1), Read("""{ "year": 0 }""").ToMilliseconds(TimeCalendar.Utc));
        Assert.Equal(TimeTestSupport.Ms(100, 1, 1), Read("""{ "year": 100 }""").ToMilliseconds(TimeCalendar.Utc));
        Assert.True(double.IsNaN(Read("""{ "year": 10000 }""").ToMilliseconds(TimeCalendar.Utc)));
        Assert.True(double.IsNaN(Read("""{ "year": -5 }""").ToMilliseconds(TimeCalendar.Utc)));
    }

    /// <summary>
    ///     Validates that an invalid object is rejected with the member that is wrong.
    /// </summary>
    [Theory]
    [InlineData("""{ "month": "Tomorrow" }""", "month")]
    [InlineData("""{ "month": true }""", "month")]
    [InlineData("""{ "month": "" }""", "month")]
    [InlineData("""{ "quarter": "x" }""", "quarter")]
    [InlineData("""{ "day": "Yesterday" }""", "day")]
    [InlineData("""{ "year": "2020" }""", "year")]
    [InlineData("""{ "date": "1" }""", "date")]
    [InlineData("""{ "hours": null }""", "hours")]
    [InlineData("""{ "foo": 1 }""", "foo")]
    [InlineData("""{ "utc": "yes" }""", "utc")]
    [InlineData("""{ "week": 3 }""", "week")]
    public void DateTimeObject_TryRead_RejectsInvalidMembers(string json, string member)
    {
        // Arrange: the object node
        var node = TimeTestSupport.Node(json);

        // Act: read it
        var ok = DateTimeObject.TryRead(node, out var value, out var error, out var at);

        // Assert: rejected, with a message and the offending member
        Assert.False(ok);
        Assert.Null(value);
        Assert.False(string.IsNullOrEmpty(error));
        Assert.NotNull(at);
        Assert.Equal(member, at.Name);
    }

    /// <summary>
    ///     Validates that a value that is not an object is rejected at the node itself.
    /// </summary>
    [Theory]
    [InlineData("5")]
    [InlineData("\"2020-01-01\"")]
    [InlineData("[1, 2]")]
    public void DateTimeObject_TryRead_RejectsNonObjects(string json)
    {
        // Arrange: the node
        var node = TimeTestSupport.Node(json);

        // Act: read it
        var ok = DateTimeObject.TryRead(node, out _, out var error, out var at);

        // Assert: rejected at the node
        Assert.False(ok);
        Assert.False(string.IsNullOrEmpty(error));
        Assert.Same(node, at);
    }

    /// <summary>
    ///     Validates the rule for a bare number compared with a field that has a single local unit: below 10000 it is that part, otherwise it is milliseconds.
    /// </summary>
    [Fact]
    public void DateTimeObject_FromScalar_NumbersFollowTheUnit()
    {
        // Arrange: the zone calendar is UTC
        var utc = TimeCalendar.Utc;

        // Act / Assert: 5 against hours is 05:00 on 2012-01-01, 3 against month is March, 2024 against year is 2024
        Assert.Equal(TimeTestSupport.Ms(2012, 1, 1, 5), DateTimeObject.FromScalar(5, Unit("hours"), utc));
        Assert.Equal(TimeTestSupport.Ms(2012, 3, 1), DateTimeObject.FromScalar(3, Unit("month"), utc));
        Assert.Equal(TimeTestSupport.Ms(2024, 1, 1), DateTimeObject.FromScalar(2024, Unit("year"), utc));
        Assert.Equal(TimeTestSupport.Ms(2012, 4, 1), DateTimeObject.FromScalar(2, Unit("quarter"), utc));
        Assert.Equal(TimeTestSupport.Ms(2012, 1, 30), DateTimeObject.FromScalar(30, Unit("date"), utc));
        Assert.Equal(TimeTestSupport.Ms(2012, 1, 1), DateTimeObject.FromScalar(7, Unit("day"), utc));
        Assert.Equal(TimeTestSupport.Ms(2012, 1, 1, 0, 7), DateTimeObject.FromScalar(7, Unit("minutes"), utc));

        // Assert: week and dayofyear have no datetime key, so they give the default date
        Assert.Equal(TimeTestSupport.Ms(2012, 1, 1), DateTimeObject.FromScalar(5, Unit("week"), utc));

        // Assert: large numbers, other units and no unit are milliseconds
        Assert.Equal(1_709_251_200_000.0, DateTimeObject.FromScalar(1_709_251_200_000.0, Unit("month"), utc));
        Assert.Equal(5.0, DateTimeObject.FromScalar(5, Unit("yearmonth"), utc));
        Assert.Equal(5.0, DateTimeObject.FromScalar(5, Unit("utcmonth"), utc));
        Assert.Equal(5.0, DateTimeObject.FromScalar(5, null, utc));
        Assert.True(double.IsNaN(DateTimeObject.FromScalar(1e300, Unit("month"), utc)));
    }

    /// <summary>
    ///     Validates the rule for a string compared with a field that has a single local unit: a date is a date, a name is the part.
    /// </summary>
    [Fact]
    public void DateTimeObject_FromScalar_StringsFollowTheUnit()
    {
        // Arrange: the zone calendar is UTC and a date parser that knows nothing, and one that knows everything
        var utc = TimeCalendar.Utc;
        static double NotADate(string text) => double.NaN;
        static double AlwaysADate(string text) => 123;

        // Act / Assert: month and day names are parts
        Assert.Equal(TimeTestSupport.Ms(2012, 3, 1), DateTimeObject.FromScalar("Mar", Unit("month"), utc, NotADate));
        Assert.Equal(TimeTestSupport.Ms(2012, 3, 1), DateTimeObject.FromScalar("Mar", Unit("month"), utc, null));
        Assert.Equal(TimeTestSupport.Ms(2012, 1, 3), DateTimeObject.FromScalar("Tuesday", Unit("day"), utc, NotADate));
        Assert.Equal(TimeTestSupport.Ms(2012, 1, 1, 5), DateTimeObject.FromScalar("5", Unit("hours"), utc, NotADate));

        // Assert: a string that is a date stays a date
        Assert.Equal(123.0, DateTimeObject.FromScalar("Mar", Unit("month"), utc, AlwaysADate));
        Assert.Equal(123.0, DateTimeObject.FromScalar("2020-01-01", null, utc, AlwaysADate));

        // Assert: anything else is not a date
        Assert.True(double.IsNaN(DateTimeObject.FromScalar("abc", Unit("month"), utc, NotADate)));
        Assert.True(double.IsNaN(DateTimeObject.FromScalar("Mar", Unit("yearmonth"), utc, NotADate)));
        Assert.True(double.IsNaN(DateTimeObject.FromScalar("Mar", null, utc, null)));
    }

    /// <summary>
    ///     Validates that names are matched the same way under a Turkish and a German culture (the Turkish upper-case I must not break FRIDAY).
    /// </summary>
    [Fact]
    public void DateTimeObject_NamesAreCultureIndependent()
    {
        // Arrange: a summary of objects whose names contain the letter I
        static string Summarize() => string.Join(
            "|",
            Read("""{ "day": "FRIDAY" }""").ToMilliseconds(TimeCalendar.Utc),
            Read("""{ "month": "SEPTEMBER" }""").ToMilliseconds(TimeCalendar.Utc),
            DateTimeObject.FromScalar("FRI", Unit("day"), TimeCalendar.Utc, null));

        // Act: run under the three cultures
        var invariant = ExpressionTestSupport.UnderCulture(string.Empty, Summarize);
        var turkish = ExpressionTestSupport.UnderCulture("tr-TR", Summarize);
        var german = ExpressionTestSupport.UnderCulture("de-DE", Summarize);

        // Assert: Friday is January 6 2012, September is September 1 2012, the same in every culture
        var friday = TimeTestSupport.Ms(2012, 1, 6);
        var september = TimeTestSupport.Ms(2012, 9, 1);
        Assert.Equal($"{friday}|{september}|{friday}".Replace(',', '.'), invariant.Replace(',', '.'));
        Assert.Equal(invariant, turkish);
        Assert.Equal(invariant, german);
    }

    /// <summary>
    ///     Validates the source of the call that builds an object: the normalized parts (the month from 0, the quarter as three months, the weekday as a date), the defaults,
    ///     <c>utc(...)</c> for a UTC object and the invariant form of every number.
    /// </summary>
    [Theory]
    [InlineData("""{ "year": 2024, "month": "Mar", "date": 5 }""", "datetime(2024, 2, 5, 0, 0, 0, 0)")]
    [InlineData("""{ "month": 3 }""", "datetime(2012, 2, 1, 0, 0, 0, 0)")]
    [InlineData("""{ "year": 2024, "quarter": 3 }""", "datetime(2024, 6, 1, 0, 0, 0, 0)")]
    [InlineData("""{ "day": "Monday" }""", "datetime(2012, 0, 2, 0, 0, 0, 0)")]
    [InlineData("""{ "day": 0 }""", "datetime(2012, 0, 1, 0, 0, 0, 0)")]
    [InlineData("""{ "year": 2024, "hours": 12, "minutes": 30, "seconds": 15, "milliseconds": 250 }""", "datetime(2024, 0, 1, 12, 30, 15, 250)")]
    [InlineData("""{ "year": 2024, "month": 12, "date": 31, "utc": true }""", "utc(2024, 11, 31, 0, 0, 0, 0)")]
    [InlineData("""{ "year": 2024, "date": 32 }""", "datetime(2024, 0, 32, 0, 0, 0, 0)")]
    [InlineData("""{ "seconds": 0.5 }""", "datetime(2012, 0, 1, 0, 0, 0.5, 0)")]
    public void DateTimeObject_ToExpression_WritesTheNormalizedParts(string json, string expected) =>
        Assert.Equal(expected, Read(json).ToExpression());

    /// <summary>
    ///     Validates that the expression gives the same instant as the object does, in a zone and in UTC (the expression is what the predicates evaluate).
    /// </summary>
    [Fact]
    public void DateTimeObject_ToExpression_IsTheSameInstantAsToMilliseconds()
    {
        // Arrange
        var zone = TimeTestSupport.Calendar(TimeTestSupport.Fixed(5, 30));
        var value = Read("""{ "year": 2024, "month": "Mar", "date": 5, "hours": 8 }""");

        // Act
        var viaExpression = ExpressionTestSupport.EvalNumber("time(" + value.ToExpression() + ")");

        // Assert: the expression is evaluated in UTC by the helper; in the zone the instant is the local wall time
        Assert.Equal(value.ToMilliseconds(TimeCalendar.Utc), viaExpression);
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 5, 8) - ((5 * 3_600_000) + (30 * 60_000)), value.ToMilliseconds(zone));
    }

    /// <summary>
    ///     Validates the source of the call for a single part of a time unit: the number 3 of a month is index 2, a name is found, the weekday is the date, and a value
    ///     that is not valid for the part gives none.
    /// </summary>
    [Fact]
    public void DateTimeObject_TryUnitPartExpression_WritesThePartWithDefaults()
    {
        // Act / Assert
        Assert.True(DateTimeObject.TryUnitPartExpression("month", 3, null, out var month));
        Assert.Equal("datetime(2012, 2, 1, 0, 0, 0, 0)", month);
        Assert.True(DateTimeObject.TryUnitPartExpression("month", double.NaN, "Mar", out var name));
        Assert.Equal(month, name);
        Assert.True(DateTimeObject.TryUnitPartExpression("day", double.NaN, "Fri", out var day));
        Assert.Equal("datetime(2012, 0, 6, 0, 0, 0, 0)", day);
        Assert.True(DateTimeObject.TryUnitPartExpression("quarter", 2, null, out var quarter));
        Assert.Equal("datetime(2012, 3, 1, 0, 0, 0, 0)", quarter);
        Assert.True(DateTimeObject.TryUnitPartExpression("hours", 5, null, out var hours));
        Assert.Equal("datetime(2012, 0, 1, 5, 0, 0, 0)", hours);
        Assert.True(DateTimeObject.TryUnitPartExpression("week", 5, null, out var week));
        Assert.Equal("datetime(2012, 0, 1, 0, 0, 0, 0)", week);
        Assert.False(DateTimeObject.TryUnitPartExpression("month", double.NaN, "Foo", out _));
        Assert.False(DateTimeObject.TryUnitPartExpression("month", double.NaN, null, out _));
    }
}
