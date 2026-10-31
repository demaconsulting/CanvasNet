using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Tests.Expressions;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Data;

/// <summary>
///     Unit tests of <see cref="JsDateParser"/>, the <c>Date.parse</c> rules of Vega's date parsing: the ECMAScript date-time
///     string format and a documented subset of the legacy forms. The expected instants come from the base class library for
///     plain calendar dates and are worked out by hand otherwise.
/// </summary>
public class JsDateParserTests
{
    /// <summary>
    ///     Parses with a calendar and checks that the success flag agrees with the number.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="calendar">The calendar, or null for UTC.</param>
    /// <returns>The instant, or NaN when the text is not a date.</returns>
    private static double Parse(string text, TimeCalendar? calendar = null)
    {
        var ok = JsDateParser.TryParse(text, calendar ?? TimeCalendar.Utc, out var ms);
        Assert.Equal(ok, !double.IsNaN(ms));
        return ms;
    }

    /// <summary>
    ///     Validates that a date without a time is UTC in every calendar: 2024-03-05 is 1,709,596,800,000 ms.
    /// </summary>
    [Fact]
    public void JsDateParser_Iso_DateOnly_IsUtc()
    {
        // Arrange: a zone behind UTC and one ahead of it
        var behind = TimeTestSupport.Calendar(TimeTestSupport.UsLike());
        var ahead = TimeTestSupport.Calendar(TimeTestSupport.Fixed(5, 30));

        // Act: parse the year, the month and the day forms
        var day = Parse("2024-03-05", behind);
        var dayAhead = Parse("2024-03-05", ahead);
        var month = Parse("2024-03", behind);
        var year = Parse("2024", ahead);

        // Assert: always UTC midnight
        Assert.Equal(1_709_596_800_000, day);
        Assert.Equal(day, dayAhead);
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 1), month);
        Assert.Equal(TimeTestSupport.Ms(2024, 1, 1), year);
    }

    /// <summary>
    ///     Validates the mixed rule of JavaScript: a date-time without an offset is in the zone of the calendar.
    /// </summary>
    [Fact]
    public void JsDateParser_Iso_DateTimeWithoutOffset_UsesTheZone()
    {
        // Arrange: UTC, a US-like zone (-5 h in March before the change, -4 h in July) and +05:30
        var usLike = TimeTestSupport.Calendar(TimeTestSupport.UsLike());
        var indian = TimeTestSupport.Calendar(TimeTestSupport.Fixed(5, 30));

        // Act: parse local times
        var utc = Parse("2024-03-05T10:00");
        var winter = Parse("2024-03-05T10:00", usLike);
        var summer = Parse("2024-07-15T10:00:00", usLike);
        var spaced = Parse("2024-03-05 10:00:00", indian);

        // Assert: UTC = wall - offset
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 5, 10), utc);
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 5, 15), winter);
        Assert.Equal(TimeTestSupport.Ms(2024, 7, 15, 14), summer);
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 5, 4, 30), spaced);
    }

    /// <summary>
    ///     Validates that an offset is honored in every calendar: <c>Z</c>, <c>+02:00</c>, <c>-05:30</c>, <c>+0200</c> and <c>+02</c>.
    /// </summary>
    [Fact]
    public void JsDateParser_Iso_Offsets_AreHonored()
    {
        // Arrange: a calendar that must not matter
        var usLike = TimeTestSupport.Calendar(TimeTestSupport.UsLike());

        // Act: parse the offset forms
        var zulu = Parse("2024-03-05T10:00:00Z", usLike);
        var plus = Parse("2024-03-05T10:00:00+02:00", usLike);
        var minus = Parse("2024-03-05T10:00:00-05:30", usLike);
        var compact = Parse("2024-03-05T10:00+0200", usLike);
        var hoursOnly = Parse("2024-03-05T10:00:00+02", usLike);

        // Assert: UTC = wall - offset
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 5, 10), zulu);
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 5, 8), plus);
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 5, 15, 30), minus);
        Assert.Equal(plus, compact);
        Assert.Equal(plus, hoursOnly);
    }

    /// <summary>
    ///     Validates the fraction digits: any number of digits is read and truncated to milliseconds.
    /// </summary>
    /// <param name="fraction">The digits after the point.</param>
    /// <param name="milliseconds">The expected milliseconds.</param>
    [Theory]
    [InlineData("5", 500)]
    [InlineData("05", 50)]
    [InlineData("123", 123)]
    [InlineData("1239", 123)]
    [InlineData("123456789", 123)]
    [InlineData("0059", 5)]
    [InlineData("000", 0)]
    public void JsDateParser_Iso_FractionDigits_AreTruncatedToMilliseconds(string fraction, int milliseconds)
    {
        // Arrange / Act: parse a time with the fraction
        var actual = Parse("2024-03-05T10:00:00." + fraction + "Z");

        // Assert: whole seconds plus the milliseconds
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 5, 10, 0, 0, milliseconds), actual);
    }

    /// <summary>
    ///     Validates expanded years (<c>±YYYYYY</c>): years inside the range parse, and the year 0, negative years and years above 9999 do not.
    /// </summary>
    [Fact]
    public void JsDateParser_Iso_ExpandedYears()
    {
        // Arrange / Act: parse expanded forms
        var inside = Parse("+002024-03-05");
        var minusZero = Parse("-000000-01-01");
        var plusZero = Parse("+000000-01-01");
        var negative = Parse("-000001-01-01");
        var tooLarge = Parse("+010000-01-01");
        var tenThousand = Parse("10000-01-01");

        // Assert: the range is years 1 to 9999
        Assert.Equal(1_709_596_800_000, inside);
        Assert.True(double.IsNaN(minusZero));
        Assert.True(double.IsNaN(plusZero));
        Assert.True(double.IsNaN(negative));
        Assert.True(double.IsNaN(tooLarge));
        Assert.True(double.IsNaN(tenThousand));
    }

    /// <summary>
    ///     Validates the ends of the range and years below 100 (taken literally, with no 1900 shift).
    /// </summary>
    [Fact]
    public void JsDateParser_Iso_RangeBoundaries()
    {
        // Arrange / Act: parse the extremes
        var first = Parse("0001-01-01");
        var last = Parse("9999-12-31T23:59:59.999Z");
        var beyond = Parse("9999-12-31T23:59:59.999-01:00");
        var before = Parse("0001-01-01T00:00:00+01:00");
        var fifty = Parse("0050-01-01");

        // Assert: -62,135,596,800,000 is 0001-01-01 and 253,402,300,799,999 is the last millisecond of 9999
        Assert.Equal(-62_135_596_800_000, first);
        Assert.Equal(253_402_300_799_999, last);
        Assert.True(double.IsNaN(beyond));
        Assert.True(double.IsNaN(before));
        Assert.Equal(TimeTestSupport.Ms(50, 1, 1), fifty);
    }

    /// <summary>
    ///     Validates the days of a month: the leap day exists in 2024 and 2000 but not in 2023 and 2100, and day 31 of a short month does not.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="valid">Whether it is a date.</param>
    [Theory]
    [InlineData("2024-02-29", true)]
    [InlineData("2000-02-29", true)]
    [InlineData("2023-02-29", false)]
    [InlineData("2100-02-29", false)]
    [InlineData("2024-04-31", false)]
    [InlineData("2024-04-30", true)]
    [InlineData("2024-00-10", false)]
    [InlineData("2024-13-01", false)]
    [InlineData("2024-01-00", false)]
    [InlineData("2024-13-45", false)]
    public void JsDateParser_Iso_DaysOfTheMonth(string text, bool valid)
    {
        // Arrange / Act: parse
        var actual = Parse(text);

        // Assert: valid dates only
        Assert.Equal(valid, !double.IsNaN(actual));
    }

    /// <summary>
    ///     Validates the time-of-day rules: 24:00 is the end of the day (and only 24:00:00.000), and hours above 24, minutes and seconds of 60 are rejected.
    /// </summary>
    [Fact]
    public void JsDateParser_Iso_TimeOfDayRules()
    {
        // Arrange / Act: parse the edge cases
        var endOfDay = Parse("2024-03-05T24:00:00Z");
        var shortEndOfDay = Parse("2024-03-05T24:00Z");
        var withMilliseconds = Parse("2024-03-05T24:00:00.000Z");
        var late = Parse("2024-03-05T23:59:59.999Z");

        // Assert: 24:00 is the next midnight
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 6), endOfDay);
        Assert.Equal(endOfDay, shortEndOfDay);
        Assert.Equal(endOfDay, withMilliseconds);
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 5, 23, 59, 59, 999), late);
        foreach (var text in new[] { "2024-03-05T24:00:01Z", "2024-03-05T24:01Z", "2024-03-05T24:00:00.001Z", "2024-03-05T25:00Z", "2024-03-05T23:60Z", "2024-03-05T23:59:60Z", "2024-03-05T10:00+24:00", "2024-03-05T10:00+02:60" })
        {
            Assert.True(double.IsNaN(Parse(text)), text);
        }
    }

    /// <summary>
    ///     Validates texts that look like the ISO format but are not: they are not dates.
    /// </summary>
    /// <param name="text">The text.</param>
    [Theory]
    [InlineData("2024-3-5")]
    [InlineData("2024-03-05T")]
    [InlineData("2024-03-05T10")]
    [InlineData("2024-03-05T10:00:")]
    [InlineData("2024-03-05T10:00:00.")]
    [InlineData("2024-03-05T10:00:00Zx")]
    [InlineData("2024-03-05T10:00:00 +0200")]
    [InlineData("2024-03-05Z")]
    [InlineData("20240305")]
    [InlineData("2024-03-05T10:00:00+2")]
    [InlineData("\u0662\u0660\u0662\u0664-03-05")]
    public void JsDateParser_Iso_Malformed_IsRejected(string text)
    {
        // Arrange / Act / Assert: no date
        Assert.True(double.IsNaN(Parse(text)));
    }

    /// <summary>
    ///     Validates the legacy forms with a month name: they are local when they have no zone designator.
    /// </summary>
    [Fact]
    public void JsDateParser_Legacy_MonthNameForms_AreLocal()
    {
        // Arrange: UTC and a zone two hours ahead
        var ahead = TimeTestSupport.Calendar(TimeTestSupport.Fixed(2));

        // Act: parse the forms
        var monthFirst = Parse("Jan 2, 2006");
        var monthFirstAhead = Parse("Jan 2, 2006", ahead);
        var full = Parse("January 2, 2006 10:00");
        var dayFirst = Parse("2 Jan 2006");
        var dotted = Parse("Jan. 2, 2006");
        var noComma = Parse("Jan 02 2006");

        // Assert: local midnight is 22:00Z the day before in the +2 zone
        Assert.Equal(TimeTestSupport.Ms(2006, 1, 2), monthFirst);
        Assert.Equal(TimeTestSupport.Ms(2006, 1, 1, 22), monthFirstAhead);
        Assert.Equal(TimeTestSupport.Ms(2006, 1, 2, 10), full);
        Assert.Equal(monthFirst, dayFirst);
        Assert.Equal(monthFirst, dotted);
        Assert.Equal(monthFirst, noComma);
    }

    /// <summary>
    ///     Validates the long forms with a weekday and a zone: the weekday is skipped (and not validated), GMT and UTC are
    ///     zero offsets, <c>GMT+0200</c> is two hours ahead, and a parenthesized comment is ignored.
    /// </summary>
    [Fact]
    public void JsDateParser_Legacy_WeekdayAndZoneForms()
    {
        // Arrange: a calendar that must not matter when a zone is given
        var usLike = TimeTestSupport.Calendar(TimeTestSupport.UsLike());

        // Act: parse the forms (2006-01-02 was a Monday, so "Tue," is not validated)
        var toString = Parse("Mon Jan 02 2006 15:04:05 GMT+0200", usLike);
        var withComment = Parse("Mon Jan 02 2006 15:04:05 GMT+0200 (Central European Summer Time)", usLike);
        var rfc = Parse("Tue, 02 Jan 2006 15:04:05 GMT", usLike);
        var utc = Parse("Jan 2, 2006 10:00 UTC", usLike);
        var zulu = Parse("Jan 2, 2006 10:00 Z", usLike);
        var offset = Parse("Jan 2, 2006 10:00 +0530", usLike);
        var colon = Parse("Jan 2, 2006 10:00 GMT+05:30", usLike);
        var minus = Parse("Jan 2, 2006 10:00 -0500", usLike);

        // Assert: UTC = wall - offset
        Assert.Equal(TimeTestSupport.Ms(2006, 1, 2, 13, 4, 5), toString);
        Assert.Equal(toString, withComment);
        Assert.Equal(TimeTestSupport.Ms(2006, 1, 2, 15, 4, 5), rfc);
        Assert.Equal(TimeTestSupport.Ms(2006, 1, 2, 10), utc);
        Assert.Equal(utc, zulu);
        Assert.Equal(TimeTestSupport.Ms(2006, 1, 2, 4, 30), offset);
        Assert.Equal(offset, colon);
        Assert.Equal(TimeTestSupport.Ms(2006, 1, 2, 15), minus);
    }

    /// <summary>
    ///     Validates the numeric legacy forms: year/month/day and month/day/year, with times and day periods.
    /// </summary>
    [Fact]
    public void JsDateParser_Legacy_NumericFormsAndTimes()
    {
        // Arrange / Act: parse the forms
        var ymd = Parse("2006/01/02");
        var mdy = Parse("1/2/2006");
        var pm = Parse("12/31/1999 11:59:59 PM");
        var midnight = Parse("1/2/2006 12:00 AM");
        var noon = Parse("1/2/2006 12:00 PM");
        var fraction = Parse("Jan 2, 2006 10:00:00.250");
        var compactPeriod = Parse("1/2/2006 3:30pm");

        // Assert: hand-derived values
        Assert.Equal(TimeTestSupport.Ms(2006, 1, 2), ymd);
        Assert.Equal(ymd, mdy);
        Assert.Equal(TimeTestSupport.Ms(1999, 12, 31, 23, 59, 59), pm);
        Assert.Equal(ymd, midnight);
        Assert.Equal(TimeTestSupport.Ms(2006, 1, 2, 12), noon);
        Assert.Equal(TimeTestSupport.Ms(2006, 1, 2, 10, 0, 0, 250), fraction);
        Assert.Equal(TimeTestSupport.Ms(2006, 1, 2, 15, 30), compactPeriod);
    }

    /// <summary>
    ///     Validates that month names match in any case, as full names or abbreviations of at least three letters.
    /// </summary>
    [Fact]
    public void JsDateParser_Legacy_NamesAreCaseInsensitive()
    {
        // Arrange / Act: parse mixed-case names
        var upper = Parse("JAN 2, 2006");
        var mixed = Parse("sEpTeMbEr 3, 2020");
        var sept = Parse("Sept 3, 2020");

        // Assert: the dates
        Assert.Equal(TimeTestSupport.Ms(2006, 1, 2), upper);
        Assert.Equal(TimeTestSupport.Ms(2020, 9, 3), mixed);
        Assert.Equal(mixed, sept);
    }

    /// <summary>
    ///     Validates the rejections: empty and white-space text, non-dates, impossible values and forms outside the documented subset.
    /// </summary>
    /// <param name="text">The text.</param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t\n")]
    [InlineData("abc")]
    [InlineData("Sunday")]
    [InlineData("Foo 2, 2006")]
    [InlineData("Ja 2, 2006")]
    [InlineData("Feb 30, 2006")]
    [InlineData("Jan 2 06")]
    [InlineData("Jan 2, 2006 25:00")]
    [InlineData("Jan 2, 2006 10:61")]
    [InlineData("Jan 2, 2006 10")]
    [InlineData("Jan 2, 2006 junk")]
    [InlineData("Jan 2, 2006 (unterminated")]
    [InlineData("1/2/2006 13:00 PM")]
    [InlineData("1/2/2006 0:30 AM")]
    [InlineData("13/1/2006")]
    [InlineData("2006/13/01")]
    [InlineData("1/2/2006 +25:00")]
    [InlineData("1/2/2006 +123")]
    [InlineData("1234567890123 Jan 2006")]
    [InlineData("Jan 2, 2006 #")]
    [InlineData("12")]
    [InlineData("+")]
    [InlineData("-")]
    public void JsDateParser_Rejects_NonDates(string text)
    {
        // Arrange / Act / Assert: no date
        Assert.True(double.IsNaN(Parse(text)));
    }

    /// <summary>
    ///     Validates the length limit: 128 characters are examined, 129 are rejected before any scanning.
    /// </summary>
    [Fact]
    public void JsDateParser_Rejects_OverLongText()
    {
        // Arrange: a valid date padded with spaces to 128 and 129 characters
        var exact = "Jan 2, 2006" + new string(' ', 128 - 11);
        var longer = "Jan 2, 2006" + new string(' ', 129 - 11);
        var megabyte = new string('1', 1 << 20);

        // Act: parse
        var accepted = Parse(exact);
        var rejected = Parse(longer);
        var huge = Parse(megabyte);

        // Assert: the limit
        Assert.Equal(128, exact.Length);
        Assert.Equal(TimeTestSupport.Ms(2006, 1, 2), accepted);
        Assert.True(double.IsNaN(rejected));
        Assert.True(double.IsNaN(huge));
    }

    /// <summary>
    ///     Validates that the parser never throws and that a success is always inside the calendar's range, for a fixed corpus of
    ///     200 texts built from fragments (including surrogate pairs, NUL characters and long digit runs).
    /// </summary>
    [Fact]
    public void JsDateParser_NeverThrows_ForArbitraryText()
    {
        // Arrange: fragments and a small deterministic generator (a linear congruential sequence)
        string[] fragments =
        [
            "2024", "-", "03", "05", "T", ":", "10", "Z", "+", "02", ".", "5", " ", "Jan", "March", "/", "PM", "GMT", ",", "(", ")",
            "\ud83d\ude00", "\ud83d", "\0", "999999999999", "00", "24", "60", "Mon", "x", "+000000", "UTC",
        ];
        var calendars = new[] { TimeCalendar.Utc, TimeTestSupport.Calendar(TimeTestSupport.UsLike()), TimeTestSupport.Calendar(TimeTestSupport.Fixed(-12)) };
        uint state = 12345;
        uint Next(int bound)
        {
            state = unchecked((state * 1_103_515_245) + 12_345);
            return (state >> 8) % (uint)bound;
        }

        // Act / Assert: all samples return, and a success is a valid instant
        for (var sample = 0; sample < 200; sample++)
        {
            var parts = (int)Next(10) + 1;
            var text = string.Concat(Enumerable.Range(0, parts).Select(_ => fragments[Next(fragments.Length)]));
            foreach (var calendar in calendars)
            {
                var ok = JsDateParser.TryParse(text, calendar, out var ms);
                Assert.Equal(ok, !double.IsNaN(ms));
                Assert.True(!ok || TimeCalendar.IsValid(ms), text);
            }
        }
    }

    /// <summary>
    ///     Validates that the parser ignores the current culture: month names containing the letter i (<c>APRIL</c>) match under
    ///     a Turkish culture, and the digits are ASCII.
    /// </summary>
    [Fact]
    public void JsDateParser_IgnoresTheCurrentCulture()
    {
        // Arrange: texts of the ISO and the legacy kind
        static string ParseAll() =>
            string.Join("|", new[] { "APRIL 1, 2020", "April 1, 2020 10:00 PM", "2020-04-01T10:00:00Z" }.Select(t =>
                ((long)Parse(t)).ToString(System.Globalization.CultureInfo.InvariantCulture)));

        // Act: run under the invariant, Turkish and German cultures
        var invariant = ExpressionTestSupport.UnderCulture(string.Empty, ParseAll);
        var turkish = ExpressionTestSupport.UnderCulture("tr-TR", ParseAll);
        var german = ExpressionTestSupport.UnderCulture("de-DE", ParseAll);

        // Assert: 2020-04-01 is day 18,353 (1,585,699,200,000 ms)
        Assert.Equal("1585699200000|1585778400000|1585735200000", invariant);
        Assert.Equal(invariant, turkish);
        Assert.Equal(invariant, german);
    }
}
