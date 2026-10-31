using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Tests.Expressions;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.SpecModel;

/// <summary>
///     Unit tests of <see cref="PredicateSource"/> for the predicates with a time unit or a date-time object: the expression text, written out by hand from
///     Vega-Lite's <c>fieldFilterExpression</c> (the instant of the unit's parts of the field on the left, <c>time(datetime(...))</c> of the values on the right).
/// </summary>
public class PredicateSourceTimeTests
{
    /// <summary>
    ///     Parses a specification whose only transform is a filter on a predicate and returns the predicate node.
    /// </summary>
    /// <param name="predicate">The JSON text of the predicate.</param>
    /// <returns>The <c>filter</c> value in the parsed tree.</returns>
    private static SpecNode Predicate(string predicate)
    {
        var json = """{ "data": { "values": [ { "d": 1 } ] }, "transform": [ { "filter": """ + predicate + """ } ], "mark": "point" }""";
        return SpecParser.Parse(json).Root["transform"]!.Items[0]["filter"]!;
    }

    /// <summary>
    ///     Builds the expression of a predicate.
    /// </summary>
    /// <param name="predicate">The JSON text of the predicate.</param>
    /// <returns>The source text.</returns>
    private static string Source(string predicate)
    {
        Assert.True(PredicateSource.TryBuild(Predicate(predicate), out var source));
        return source;
    }

    /// <summary>
    ///     Validates the expression of an equality with a time unit (month): the month of the field (in the reference year 2012, on the first, at midnight)
    ///     equals the month of the value; the name <c>Mar</c> is month index 2.
    /// </summary>
    [Fact]
    public void PredicateSource_TimeUnit_Equal_Month() =>
        Assert.Equal(
            """(time(datetime(2012, month(datum["d"]), 1, 0, 0, 0, 0))===time(datetime(2012, 2, 1, 0, 0, 0, 0)))""",
            Source("""{ "field": "d", "timeUnit": "month", "equal": { "month": "Mar" } }"""));

    /// <summary>
    ///     Validates a range of a multi-part unit: the parts year and month of the field, the two ends as dates (month 2 is index 1, month 4 is index 3) in
    ///     either order (<c>min</c> and <c>max</c> order them).
    /// </summary>
    [Fact]
    public void PredicateSource_TimeUnit_Range_YearMonth() =>
        Assert.Equal(
            """(time(datetime(year(datum["d"]), month(datum["d"]), 1, 0, 0, 0, 0)) >= min(time(datetime(2024, 1, 1, 0, 0, 0, 0)), time(datetime(2024, 3, 1, 0, 0, 0, 0))) && time(datetime(year(datum["d"]), month(datum["d"]), 1, 0, 0, 0, 0)) <= max(time(datetime(2024, 1, 1, 0, 0, 0, 0)), time(datetime(2024, 3, 1, 0, 0, 0, 0))))""",
            Source("""{ "field": "d", "timeUnit": "yearmonth", "range": [ { "year": 2024, "month": 2 }, { "year": 2024, "month": 4 } ] }"""));

    /// <summary>
    ///     Validates that a range with an open end compares with the other end only, and that a range with two open ends holds.
    /// </summary>
    [Fact]
    public void PredicateSource_TimeUnit_Range_OpenEnds()
    {
        // Act / Assert
        Assert.Equal(
            """(time(datum["d"]) >= time(datetime(2024, 0, 1, 0, 0, 0, 0)))""",
            Source("""{ "field": "d", "range": [ { "year": 2024 }, null ] }"""));
        Assert.Equal(
            """(time(datum["d"]) <= time(datetime(2024, 0, 1, 0, 0, 0, 0)))""",
            Source("""{ "field": "d", "range": [ null, { "year": 2024 } ] }"""));
        Assert.Equal("(true)", Source("""{ "field": "d", "range": [ null, null ], "timeUnit": "year" }"""));
    }

    /// <summary>
    ///     Validates a membership test of a weekday unit: the date of the field is the weekday plus one in January 2012, and the day names are the weekday of
    ///     that month (Monday is the second, Friday the sixth of January 2012).
    /// </summary>
    [Fact]
    public void PredicateSource_TimeUnit_OneOf_Day() =>
        Assert.Equal(
            """(indexof([time(datetime(2012, 0, 2, 0, 0, 0, 0)), time(datetime(2012, 0, 6, 0, 0, 0, 0))], time(datetime(2012, 0, day(datum["d"])+1, 0, 0, 0, 0))) !== -1)""",
            Source("""{ "field": "d", "timeUnit": "day", "oneOf": [ "Mon", "Fri" ] }"""));

    /// <summary>
    ///     Validates that a UTC unit uses the UTC functions and <c>utc(...)</c>, and a UTC date-time object <c>utc(...)</c> too.
    /// </summary>
    [Fact]
    public void PredicateSource_TimeUnit_Utc_Uses_utc_functions() =>
        Assert.Equal(
            """(time(utc(2012, utcmonth(datum["d"]), 1, 0, 0, 0, 0))===time(utc(2012, 2, 1, 0, 0, 0, 0)))""",
            Source("""{ "field": "d", "timeUnit": "utcmonth", "equal": { "month": 3, "utc": true } }"""));

    /// <summary>
    ///     Validates that a quarter is written as three times its zero-based number in the month place, and that a month wins when a unit has both.
    /// </summary>
    [Fact]
    public void PredicateSource_TimeUnit_Quarter_UsesMonthTimesThree()
    {
        // Act / Assert: the second quarter is month index 3
        Assert.Equal(
            """(time(datetime(2012, (quarter(datum["d"])-1)*3, 1, 0, 0, 0, 0))===time(datetime(2012, 3, 1, 0, 0, 0, 0)))""",
            Source("""{ "field": "d", "timeUnit": "quarter", "equal": { "quarter": 2 } }"""));
        Assert.Equal(
            """(time(datetime(year(datum["d"]), (quarter(datum["d"])-1)*3, 1, 0, 0, 0, 0))===time(datetime(2024, 3, 1, 0, 0, 0, 0)))""",
            Source("""{ "field": "d", "timeUnit": "yearquarter", "equal": { "year": 2024, "quarter": 2 } }"""));
    }

    /// <summary>
    ///     Validates that a comparison with a date-time object and no time unit compares the instant of the field itself.
    /// </summary>
    [Fact]
    public void PredicateSource_DatetimeWithoutUnit_WrapsLeftInTime()
    {
        // Act / Assert: month 2 is index 1
        Assert.Equal(
            """(time(datum["d"])>=time(datetime(2024, 1, 1, 0, 0, 0, 0)))""",
            Source("""{ "field": "d", "gte": { "year": 2024, "month": 2 } }"""));
        Assert.Equal(
            """(time(datum["a"]["b"])<time(datetime(2024, 0, 1, 12, 30, 15, 250)))""",
            Source("""{ "field": "a.b", "lt": { "year": 2024, "hours": 12, "minutes": 30, "seconds": 15, "milliseconds": 250 } }"""));
    }

    /// <summary>
    ///     Validates that the parts <c>week</c> and <c>dayofyear</c> are left out of a predicate, as Vega-Lite leaves them out (the year is kept, the rest defaults).
    /// </summary>
    [Fact]
    public void PredicateSource_WeekPart_IsOmittedAndWarned()
    {
        // Act
        var source = Source("""{ "field": "d", "timeUnit": "yearweek", "equal": { "year": 2024 } }""");
        var dayOfYear = Source("""{ "field": "d", "timeUnit": "dayofyear", "equal": { "year": 2024 } }""");

        // Assert
        Assert.Equal("""(time(datetime(year(datum["d"]), 0, 1, 0, 0, 0, 0))===time(datetime(2024, 0, 1, 0, 0, 0, 0)))""", source);
        Assert.Equal("""(time(datetime(2012, 0, 1, 0, 0, 0, 0))===time(datetime(2024, 0, 1, 0, 0, 0, 0)))""", dayOfYear);
    }

    /// <summary>
    ///     Validates a text value of a field with a unit that is not a single one: a text that is a date becomes a <c>datetime</c> call that reads it, and one that is not
    ///     a date becomes the null instant.
    /// </summary>
    [Fact]
    public void PredicateSource_StringValueWithUnit_UsesDatetimeLiteral()
    {
        // Act / Assert
        Assert.Equal(
            """(time(datetime(year(datum["d"]), month(datum["d"]), date(datum["d"]), 0, 0, 0, 0))===time(datetime("2024-03-05")))""",
            Source("""{ "field": "d", "timeUnit": "yearmonthdate", "equal": "2024-03-05" }"""));
        Assert.Equal(
            """(time(datetime(year(datum["d"]), month(datum["d"]), 1, 0, 0, 0, 0))===time(null))""",
            Source("""{ "field": "d", "timeUnit": "yearmonth", "equal": "soon" }"""));
    }

    /// <summary>
    ///     Validates the rule for a number or a text that is compared with a single local unit: below 10000 it is the part of the unit (March is index 2 for the
    ///     number 3 and the text <c>Mar</c>, 5 is the hour 5), otherwise a number is milliseconds and is written as it is; a text that is a date is read as one.
    /// </summary>
    [Fact]
    public void PredicateSource_NumberBelow10000WithSingleUnit_UsesThePart()
    {
        // Act / Assert
        Assert.Equal(
            """(time(datetime(2012, month(datum["d"]), 1, 0, 0, 0, 0))===time(datetime(2012, 2, 1, 0, 0, 0, 0)))""",
            Source("""{ "field": "d", "timeUnit": "month", "equal": 3 }"""));
        Assert.Equal(
            """(time(datetime(2012, month(datum["d"]), 1, 0, 0, 0, 0))===time(datetime(2012, 2, 1, 0, 0, 0, 0)))""",
            Source("""{ "field": "d", "timeUnit": "month", "equal": "Mar" }"""));
        Assert.Equal(
            """(time(datetime(2012, 0, 1, hours(datum["d"]), 0, 0, 0))===time(datetime(2012, 0, 1, 5, 0, 0, 0)))""",
            Source("""{ "field": "d", "timeUnit": "hours", "equal": 5 }"""));
        Assert.Equal(
            """(time(datetime(2012, month(datum["d"]), 1, 0, 0, 0, 0))===1709596800000)""",
            Source("""{ "field": "d", "timeUnit": "month", "equal": 1709596800000 }"""));
        Assert.Equal(
            """(time(datetime(year(datum["d"]), month(datum["d"]), 1, 0, 0, 0, 0))===5)""",
            Source("""{ "field": "d", "timeUnit": "yearmonth", "equal": 5 }"""));
    }

    /// <summary>
    ///     Validates the predicates that stay as they were: <c>valid</c> with a time unit tests the field itself, a <c>maxbins</c> unit and a binned unit
    ///     are ignored, and an expression reference as a value is written in parentheses.
    /// </summary>
    [Fact]
    public void PredicateSource_UnitsThatApplyNothingAndExpressionValuesStayPlain()
    {
        // Act / Assert
        Assert.Equal("""isValid(datum["d"])""", Source("""{ "field": "d", "timeUnit": "year", "valid": true }"""));
        Assert.Equal("""(datum["d"] === 3)""", Source("""{ "field": "d", "timeUnit": { "maxbins": 10 }, "equal": 3 }"""));
        Assert.Equal("""(datum["d"] === 3)""", Source("""{ "field": "d", "timeUnit": "binnedyearmonth", "equal": 3 }"""));
        Assert.Equal("""(datum["d"] === (1+1))""", Source("""{ "field": "d", "equal": { "expr": "1+1" } }"""));
    }

    /// <summary>
    ///     Validates which predicates are written with the time functions, and that the reader of the unit ignores a binned unit and the maxbins form.
    /// </summary>
    [Fact]
    public void PredicateSource_IsTimeAware_AndReadUnit()
    {
        // Act / Assert
        Assert.True(PredicateSource.IsTimeAware(Predicate("""{ "field": "d", "timeUnit": "year", "equal": 2020 }""")));
        Assert.True(PredicateSource.IsTimeAware(Predicate("""{ "field": "d", "equal": { "year": 2020 } }""")));
        Assert.True(PredicateSource.IsTimeAware(Predicate("""{ "field": "d", "oneOf": [ 1, { "year": 2020 } ] }""")));
        Assert.False(PredicateSource.IsTimeAware(Predicate("""{ "field": "d", "equal": 2020 }""")));
        Assert.False(PredicateSource.IsTimeAware(Predicate("""{ "field": "d", "timeUnit": "year", "valid": true }""")));
        Assert.False(PredicateSource.IsTimeAware(Predicate("""{ "field": "d", "timeUnit": { "maxbins": 3 }, "equal": 2020 }""")));
        Assert.False(PredicateSource.IsTimeAware(Predicate("\"datum.d > 1\"")));
        Assert.Equal(["year", "month"], PredicateSource.ReadUnit(Predicate("""{ "field": "d", "timeUnit": "yearmonth", "equal": 1 }"""))!.Parts);
        Assert.Null(PredicateSource.ReadUnit(Predicate("""{ "field": "d", "equal": 1 }""")));
        Assert.Null(PredicateSource.ReadUnit(Predicate("""{ "field": "d", "timeUnit": "binnedyearmonth", "equal": 1 }""")));
    }

    /// <summary>
    ///     Validates that the source text does not depend on the culture of the thread: the numbers are in the invariant form and the day names of the objects are matched without
    ///     the culture (the Turkish upper-case I must not break <c>FRIDAY</c>).
    /// </summary>
    [Fact]
    public void PredicateSource_TimePredicates_AreCultureIndependent()
    {
        // Arrange
        static string Build() => string.Join(
            "|",
            Source("""{ "field": "d", "timeUnit": "seconds", "equal": 0.5 }"""),
            Source("""{ "field": "d", "timeUnit": "day", "equal": "FRIDAY" }"""),
            Source("""{ "field": "d", "gt": { "year": 2024, "month": "SEPTEMBER", "seconds": 1.5 } }"""));

        // Act
        var invariant = ExpressionTestSupport.UnderCulture(string.Empty, Build);
        var turkish = ExpressionTestSupport.UnderCulture("tr-TR", Build);
        var german = ExpressionTestSupport.UnderCulture("de-DE", Build);

        // Assert: Friday is the sixth of January 2012 (date index 6), 1.5 and 0.5 keep their point
        Assert.Contains("time(datetime(2012, 0, 6, 0, 0, 0, 0))", invariant, StringComparison.Ordinal);
        Assert.Contains("1.5, 0)", invariant, StringComparison.Ordinal);
        Assert.Equal(invariant, turkish);
        Assert.Equal(invariant, german);
    }

    /// <summary>
    ///     Validates the argument checks of the new members.
    /// </summary>
    [Fact]
    public void PredicateSource_TimeMembers_RejectNull()
    {
        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => PredicateSource.IsTimeAware(null!));
        Assert.Throws<ArgumentNullException>(() => PredicateSource.ReadUnit(null!));
    }
}
