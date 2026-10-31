using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Tests.Expressions;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Data;

/// <summary>
///     Unit tests of the d3-time-format parser of <see cref="TimeFormat.TryParse"/>. The expected instants are worked out by
///     hand (or computed with the base class library for plain calendar dates), never taken from the JavaScript library.
/// </summary>
public class TimeFormatParseTests
{
    /// <summary>The instant 2024-03-05T13:04:09.007Z (see <see cref="TimeFormatTests"/>).</summary>
    private const double Instant = 1_709_643_849_007;

    /// <summary>The instant 2024-03-05T00:00Z: day 19,787.</summary>
    private const double March5 = 1_709_596_800_000;

    /// <summary>
    ///     Parses with the UTC calendar.
    /// </summary>
    /// <param name="specifier">The specifier.</param>
    /// <param name="text">The text.</param>
    /// <returns>The instant, or NaN when the parse failed.</returns>
    private static double Parse(string specifier, string text) => Parse(specifier, text, TimeCalendar.Utc);

    /// <summary>
    ///     Parses with a calendar and checks that the success flag agrees with the number.
    /// </summary>
    /// <param name="specifier">The specifier.</param>
    /// <param name="text">The text.</param>
    /// <param name="calendar">The calendar.</param>
    /// <returns>The instant, or NaN when the parse failed.</returns>
    private static double Parse(string specifier, string text, TimeCalendar calendar)
    {
        var ok = TimeFormat.TryParse(specifier, text, calendar, out var ms);
        Assert.Equal(ok, !double.IsNaN(ms));
        return ms;
    }

    /// <summary>
    ///     Validates that formatting with a specifier and parsing the text with the same specifier gives the instant back
    ///     (truncated to what the specifier shows).
    /// </summary>
    /// <param name="specifier">The specifier.</param>
    /// <param name="expected">The instant that survives the round trip.</param>
    [Theory]
    [InlineData("%Y-%m-%d %H:%M:%S.%L", Instant)]
    [InlineData("%Y-%j %H:%M:%S.%L", Instant)]
    [InlineData("%Y-%m-%d %H:%M:%S.%f", Instant)]
    [InlineData("%c", Instant - 7)]
    [InlineData("%x %X", Instant - 7)]
    [InlineData("%Q", Instant)]
    [InlineData("%s", Instant - 7)]
    [InlineData("%Y-%m-%dT%H:%M:%S%Z", Instant - 7)]
    [InlineData("%A, %B %d, %Y", March5)]
    [InlineData("%a %b %e %Y", March5)]
    [InlineData("%y-%m-%d", March5)]
    [InlineData("%Y %U %w", March5)]
    [InlineData("%Y %W %w", March5)]
    [InlineData("%Y W%V %w", March5)]
    [InlineData("%Y %U %u", March5)]
    [InlineData("%-d/%-m/%Y", March5)]
    public void TimeFormatParse_RoundTripsEveryDirectiveItFormats(string specifier, double expected)
    {
        // Arrange: the text of the anchor instant for this specifier
        var text = TimeFormat.Compile(specifier).Format(Instant, TimeCalendar.Utc, 1000);

        // Act: parse it back
        var actual = Parse(specifier, text);

        // Assert: the instant, as far as the specifier shows it
        Assert.Equal(expected, actual);
    }

    /// <summary>
    ///     Validates that a quarter gives the month when no month is read: "2024-Q3" is July 1 (month 6), and an explicit month wins.
    /// </summary>
    [Fact]
    public void TimeFormatParse_Quarter_GivesTheFirstMonthUnlessAMonthIsRead()
    {
        // Arrange / Act: parse a quarter alone and with a month
        var quarter = Parse("%Y-Q%q", "2024-Q3");
        var withMonth = Parse("%Y-Q%q-%m", "2024-Q3-02");

        // Assert: July 1 (month 6, the first month of quarter 3), and February when a month is read
        Assert.Equal(TimeTestSupport.Ms(2024, 7, 1), quarter);
        Assert.Equal(TimeTestSupport.Ms(2024, 2, 1), withMonth);
    }

    /// <summary>
    ///     Validates the week-based directives by hand: 2024-01-01 is a Monday, so Sunday-week 9 / weekday 2 is January 65
    ///     (March 5), Sunday-week 9 / ISO weekday 7 is January 63 (Sunday March 3) and ISO week 10 without a weekday is Monday March 4.
    /// </summary>
    [Fact]
    public void TimeFormatParse_WeekDirectives_ConvertToDays()
    {
        // Arrange / Act: parse week numbers with and without weekdays
        var sunday = Parse("%Y %U %u", "2024 09 7");
        var isoDefault = Parse("%Y W%V", "2024 W10");
        var mondayDefault = Parse("%Y %W", "2024 10");
        var sundayDefault = Parse("%Y %U", "2024 09");

        // Assert: the hand-derived days (a day is 86,400,000 ms)
        const double Day = 86_400_000.0;
        Assert.Equal(March5 - (2 * Day), sunday);
        Assert.Equal(March5 - Day, isoDefault);
        Assert.Equal(March5 - Day, mondayDefault);
        Assert.Equal(March5 - (2 * Day), sundayDefault);
    }

    /// <summary>
    ///     Validates that an ISO week number outside 1 to 53 fails.
    /// </summary>
    /// <param name="text">The text.</param>
    [Theory]
    [InlineData("2024 W00")]
    [InlineData("2024 W54")]
    [InlineData("2024 W99")]
    public void TimeFormatParse_IsoWeekOutOfRange_Fails(string text)
    {
        // Arrange / Act / Assert: no result
        Assert.True(double.IsNaN(Parse("%Y W%V", text)));
    }

    /// <summary>
    ///     Validates the common day-month-year order.
    /// </summary>
    [Fact]
    public void TimeFormatParse_DayMonthYear()
    {
        // Arrange / Act: 05/03/2024 as day, month, year
        var actual = Parse("%d/%m/%Y", "05/03/2024");

        // Assert: March 5
        Assert.Equal(March5, actual);
    }

    /// <summary>
    ///     Validates that names and the day period are matched ignoring case: "jan 02, 2006 01:45 pm" is 2006-01-02T13:45Z.
    /// </summary>
    [Fact]
    public void TimeFormatParse_MonthNamesAndPeriods_CaseInsensitive()
    {
        // Arrange / Act: lower-case, upper-case and mixed-case input
        var lower = Parse("%b %d, %Y %I:%M %p", "jan 02, 2006 01:45 pm");
        var upper = Parse("%B %d, %Y %I:%M %p", "JANUARY 02, 2006 01:45 PM");
        var mixed = Parse("%A %b %d, %Y %I:%M %p", "mOnDaY Jan 02, 2006 01:45 Pm");

        // Assert: all the same instant
        var expected = TimeTestSupport.Ms(2006, 1, 2, 13, 45);
        Assert.Equal(expected, lower);
        Assert.Equal(expected, upper);
        Assert.Equal(expected, mixed);
    }

    /// <summary>
    ///     Validates the twelve-hour clock without a date: the default is 1900-01-01, so "01:45 PM" is 1900-01-01T13:45Z
    ///     (-2,208,988,800,000 + 49,500,000 = -2,208,939,300,000), and 12 AM / 12 PM map to 0 and 12.
    /// </summary>
    [Fact]
    public void TimeFormatParse_TwelveHourWithoutDate_UsesTheDefaultYear1900()
    {
        // Arrange / Act: parse times of day
        var afternoon = Parse("%I:%M %p", "01:45 PM");
        var midnight = Parse("%I:%M %p", "12:30 AM");
        var noon = Parse("%I:%M %p", "12:30 PM");

        // Assert: hand-derived and checked against the base class library
        Assert.Equal(-2_208_939_300_000, afternoon);
        Assert.Equal(TimeTestSupport.Ms(1900, 1, 1, 13, 45), afternoon);
        Assert.Equal(TimeTestSupport.Ms(1900, 1, 1, 0, 30), midnight);
        Assert.Equal(TimeTestSupport.Ms(1900, 1, 1, 12, 30), noon);
    }

    /// <summary>
    ///     Validates the pivot of two-digit years: 69 to 99 are 1969 to 1999, 00 to 68 are 2000 to 2068.
    /// </summary>
    /// <param name="text">The two digits.</param>
    /// <param name="year">The expected year.</param>
    [Theory]
    [InlineData("69", 1969)]
    [InlineData("99", 1999)]
    [InlineData("68", 2068)]
    [InlineData("00", 2000)]
    [InlineData("24", 2024)]
    public void TimeFormatParse_TwoDigitYearPivot(string text, int year)
    {
        // Arrange / Act: parse January 1 of the year
        var actual = Parse("%y-%m-%d", text + "-01-01");

        // Assert: the pivot year
        Assert.Equal(TimeTestSupport.Ms(year, 1, 1), actual);
    }

    /// <summary>
    ///     Validates <c>%Z</c>: <c>±hhmm</c>, <c>±hh:mm</c>, <c>±hh</c> and <c>Z</c> make the fields UTC and apply the offset, in any calendar.
    /// </summary>
    [Fact]
    public void TimeFormatParse_ZoneDirective()
    {
        // Arrange: formats for a text with a zone, and a zone calendar that must not matter
        const string Specifier = "%Y-%m-%dT%H:%M:%S%Z";
        var usLike = TimeTestSupport.Calendar(TimeTestSupport.UsLike());

        // Act: parse offsets
        var plus = Parse(Specifier, "2024-03-05T10:00:00+0200");
        var colon = Parse(Specifier, "2024-03-05T10:00:00+02:00");
        var hoursOnly = Parse(Specifier, "2024-03-05T10:00:00+02");
        var minus = Parse(Specifier, "2024-03-05T10:00:00-0530", usLike);
        var zulu = Parse(Specifier, "2024-03-05T10:00:00Z", usLike);
        var lower = Parse(Specifier, "2024-03-05T10:00:00z");

        // Assert: UTC = wall - offset
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 5, 8), plus);
        Assert.Equal(plus, colon);
        Assert.Equal(plus, hoursOnly);
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 5, 15, 30), minus);
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 5, 10), zulu);
        Assert.True(double.IsNaN(lower));
    }

    /// <summary>
    ///     Validates d3's unanchored zone expression: a zone that is not at the start of its six-character window is found
    ///     but the position advances by the length of the match, so the text is not consumed and the parse fails.
    /// </summary>
    [Fact]
    public void TimeFormatParse_ZoneDirective_ReproducesTheUnanchoredMatch()
    {
        // Arrange / Act: a zone after another character, and a bare zone
        var shifted = Parse("%Z", "x+0200");
        var bare = Parse("%Z", "+0200");
        var zulu = Parse("%Z", "Z");

        // Assert: the bare zone shifts 1900-01-01T00:00Z by -2 hours; the shifted text fails
        Assert.True(double.IsNaN(shifted));
        Assert.Equal(TimeTestSupport.Ms(1899, 12, 31, 22), bare);
        Assert.Equal(TimeTestSupport.Ms(1900, 1, 1), zulu);
    }

    /// <summary>
    ///     Validates the timestamp directives: <c>%Q</c> is milliseconds, <c>%s</c> seconds, and <c>%s</c> adds a parsed <c>%L</c>.
    /// </summary>
    [Fact]
    public void TimeFormatParse_EpochDirectives()
    {
        // Arrange / Act: parse timestamps
        var milliseconds = Parse("%Q", "1709643849007");
        var seconds = Parse("%s", "1709643849");
        var withFraction = Parse("%s.%L", "1709643849.250");
        var tooBig = Parse("%Q", new string('9', 40));
        var negative = Parse("%Q", "-5");

        // Assert: the values, and that numbers outside the calendar's range or with a sign fail
        Assert.Equal(Instant, milliseconds);
        Assert.Equal(1_709_643_849_000, seconds);
        Assert.Equal(1_709_643_849_250, withFraction);
        Assert.True(double.IsNaN(tooBig));
        Assert.True(double.IsNaN(negative));
    }

    /// <summary>
    ///     Validates that the whole text must match: trailing text, missing text and mismatching literals fail.
    /// </summary>
    /// <param name="specifier">The specifier.</param>
    /// <param name="text">The text.</param>
    [Theory]
    [InlineData("%Y-%m-%d", "2024-03-05x")]
    [InlineData("%Y-%m-%d", "2024-03")]
    [InlineData("%Y-%m-%d", "2024-03-")]
    [InlineData("%Y", "")]
    [InlineData("", "x")]
    [InlineData("T%Y", "t2024")]
    [InlineData("%Y", "abcd")]
    [InlineData("%Y", "\u0662\u0660\u0662\u0664")]
    [InlineData("%b", "Marx")]
    [InlineData("%b", "Ma")]
    [InlineData("%p", "A")]
    [InlineData("%k", "5")]
    [InlineData("%", "5")]
    [InlineData("%-", "5")]
    [InlineData("%%", "x")]
    public void TimeFormatParse_TrailingOrMissingTextFails(string specifier, string text)
    {
        // Arrange / Act / Assert: no result
        Assert.True(double.IsNaN(Parse(specifier, text)));
    }

    /// <summary>
    ///     Validates the empty specifier: it matches only empty text, which gives the defaults (1900-01-01T00:00Z).
    /// </summary>
    [Fact]
    public void TimeFormatParse_EmptySpecifier_MatchesEmptyTextOnly()
    {
        // Arrange / Act / Assert: the defaults for empty text
        Assert.Equal(TimeTestSupport.Ms(1900, 1, 1), Parse(string.Empty, string.Empty));
    }

    /// <summary>
    ///     Validates that, like d3, the parser does not validate ranges: month 13 rolls into the next year, February 30 into March,
    ///     hour 25 into the next day, minute 61 into the next hour.
    /// </summary>
    [Fact]
    public void TimeFormatParse_NoRangeValidation_FollowsD3()
    {
        // Arrange / Act: out-of-range fields
        var month = Parse("%Y-%m-%d", "2024-13-01");
        var february = Parse("%Y-%m-%d", "2024-02-30");
        var hour = Parse("%Y-%m-%d %H", "2024-03-05 25");
        var minute = Parse("%Y-%m-%d %H:%M", "2024-03-05 10:61");
        var dayZero = Parse("%Y-%m-%d", "2024-03-00");

        // Assert: JavaScript Date roll-over, as implemented by the calendar
        Assert.Equal(TimeTestSupport.Ms(2025, 1, 1), month);
        Assert.Equal(1_709_251_200_000, february);
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 6, 1), hour);
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 5, 11, 1), minute);
        Assert.Equal(TimeTestSupport.Ms(2024, 2, 29), dayZero);
    }

    /// <summary>
    ///     Validates the window rule of numbers: a directive reads at most its width, white space counts inside that window,
    ///     and JavaScript white space (including the no-break space) is allowed before the digits.
    /// </summary>
    [Fact]
    public void TimeFormatParse_NumberWindows_FollowD3()
    {
        // Arrange / Act: padded days, a short year window, three-digit milliseconds
        var spaced = Parse("%e/%m/%Y", " 5/03/2024");
        var plain = Parse("%e/%m/%Y", "5/03/2024");
        var noBreak = Parse("%e/%m/%Y", "\u00a05/03/2024");
        var emptyWindow = Parse("%d", "  5");
        var spacedYear = Parse("%Y", " 2024");
        var shortYear = Parse("%Y", " 202");
        var shortMs = Parse("%S.%L", "09.5");
        var micro = Parse("%S.%f", "09.123456");
        var wide = Parse("%S.%L", "09.1234");

        // Assert: pinned behavior
        Assert.Equal(March5, spaced);
        Assert.Equal(March5, plain);
        Assert.Equal(March5, noBreak);
        Assert.True(double.IsNaN(emptyWindow));
        Assert.True(double.IsNaN(spacedYear));
        Assert.Equal(TimeTestSupport.Ms(202, 1, 1), shortYear);
        Assert.Equal(TimeTestSupport.Ms(1900, 1, 1, 0, 0, 9, 5), shortMs);
        Assert.Equal(TimeTestSupport.Ms(1900, 1, 1, 0, 0, 9, 123), micro);
        Assert.True(double.IsNaN(wide));
    }

    /// <summary>
    ///     Validates that the padding flags are accepted and ignored by the parser.
    /// </summary>
    [Fact]
    public void TimeFormatParse_PaddingFlagsAreIgnored()
    {
        // Arrange / Act: flags before the letters
        var actual = Parse("%-d/%_m/%0Y", "5/3/2024");

        // Assert: March 5
        Assert.Equal(March5, actual);
    }

    /// <summary>
    ///     Validates the years 0 to 99: d3 builds the date in the year -1 and then sets the year, so "0004-02-29" (a leap day of
    ///     the year 4) becomes March 1 of year 4, and the year 0 is outside the calendar's range.
    /// </summary>
    [Fact]
    public void TimeFormatParse_YearsBelow100_FollowD3()
    {
        // Arrange / Act: small years
        var five = Parse("%Y-%m-%d", "0005-03-05");
        var leap = Parse("%Y-%m-%d", "0004-02-29");
        var zero = Parse("%Y-%m-%d", "0000-01-01");
        var ninetyNine = Parse("%Y-%m-%d", "0099-12-31");

        // Assert: year 5 as is, the leap day rolled over, year 0 invalid
        Assert.Equal(TimeTestSupport.Ms(5, 3, 5), five);
        Assert.Equal(TimeTestSupport.Ms(4, 3, 1), leap);
        Assert.True(double.IsNaN(zero));
        Assert.Equal(TimeTestSupport.Ms(99, 12, 31), ninetyNine);
    }

    /// <summary>
    ///     Validates that a local parse reads the fields in the calendar's zone: noon in a US-like zone in winter and summer, a
    ///     gap time (02:30 on 2024-03-10 is the instant that shows 03:30) and an overlap time (01:30 on 2024-11-03 is the earlier).
    /// </summary>
    [Fact]
    public void TimeFormatParse_LocalParseUsesTheCalendarZone()
    {
        // Arrange: a US-like zone and a specifier without a zone
        var usLike = TimeTestSupport.Calendar(TimeTestSupport.UsLike());
        const string Specifier = "%Y-%m-%d %H:%M";

        // Act: parse wall times
        var winter = Parse(Specifier, "2024-01-15 12:00", usLike);
        var summer = Parse(Specifier, "2024-07-15 12:00", usLike);
        var gap = Parse(Specifier, "2024-03-10 02:30", usLike);
        var overlap = Parse(Specifier, "2024-11-03 01:30", usLike);
        var utc = Parse(Specifier, "2024-01-15 12:00");

        // Assert: UTC = wall - offset (-5 h in winter, -4 h in summer)
        Assert.Equal(TimeTestSupport.Ms(2024, 1, 15, 17), winter);
        Assert.Equal(TimeTestSupport.Ms(2024, 7, 15, 16), summer);
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 10, 7, 30), gap);
        Assert.Equal(TimeTestSupport.Ms(2024, 11, 3, 5, 30), overlap);
        Assert.Equal(TimeTestSupport.Ms(2024, 1, 15, 12), utc);
    }

    /// <summary>
    ///     Validates that text of more than 256 characters fails at once, and that 256 characters are still examined.
    /// </summary>
    [Fact]
    public void TimeFormatParse_LongInputFailsFast()
    {
        // Arrange: a literal specifier that matches a text of exactly 256 characters
        var exact = new string('a', 256);
        var longer = new string('a', 257);

        // Act: parse both with the matching literal
        var accepted = Parse(exact, exact);
        var rejected = Parse(longer, longer);
        var longDigits = Parse("%Q", new string('1', 1000));

        // Assert: the limit is 256
        Assert.Equal(TimeTestSupport.Ms(1900, 1, 1), accepted);
        Assert.True(double.IsNaN(rejected));
        Assert.True(double.IsNaN(longDigits));
    }

    /// <summary>
    ///     Validates that a parse specifier that expands (through <c>%c</c>, <c>%x</c> and <c>%X</c>) to more than <see cref="TimeFormat.MaxTokenCount"/> pieces, or
    ///     is longer than <see cref="TimeFormat.MaxSpecifierLength"/> characters, never matches and is refused by <see cref="TimeFormat.ValidateSpecifier"/> with the
    ///     same message as the formatter, while one at the limit still parses.
    /// </summary>
    [Fact]
    public void TimeFormatParse_SpecifierLimits_FailClosedAndAreValidated()
    {
        // Arrange: hundreds of %c (13 pieces each), a text that would match the first ones, and a short specifier that expands to a few pieces
        var manyPieces = string.Concat(Enumerable.Repeat("%c", 400));
        var tooLong = new string('x', TimeFormat.MaxSpecifierLength + 1);
        var longest = new string('x', TimeFormat.MaxSpecifierLength);

        // Act
        var pieces = Parse(manyPieces, "3/5/2024, 1:04:09 PM");
        var tooLongResult = Parse(tooLong, tooLong);
        var piecesError = Assert.Throws<TimeFormatSpecifierException>(() => TimeFormat.ValidateSpecifier(manyPieces));
        var compileError = Assert.Throws<TimeFormatSpecifierException>(() => TimeFormat.Compile(manyPieces));
        var lengthError = Assert.Throws<TimeFormatSpecifierException>(() => TimeFormat.ValidateSpecifier(tooLong));

        // Assert: nothing matches past the limits, the errors are the formatter's, and a specifier at the limits is valid
        Assert.True(double.IsNaN(pieces));
        Assert.True(double.IsNaN(tooLongResult));
        Assert.Equal(compileError.Message, piecesError.Message);
        Assert.Contains("the limit of 4096 pieces", piecesError.Message, StringComparison.Ordinal);
        Assert.Contains("the limit of 1024 characters", lengthError.Message, StringComparison.Ordinal);
        TimeFormat.ValidateSpecifier(longest);
        TimeFormat.ValidateSpecifier("%c");
        Assert.Equal(Instant - 7, Parse("%c", "3/5/2024, 1:04:09 PM"));
    }

    /// <summary>
    ///     Validates that the parser never throws, whatever the specifier and the text.
    /// </summary>
    [Fact]
    public void TimeFormatParse_NeverThrows_ForArbitrarySpecifiersAndText()
    {
        // Arrange: odd specifiers and odd texts, including surrogate pairs, NUL and long digit runs
        string[] specifiers = ["%Y-%m-%d", "%c", "%Q", "%s", "%Z", "%Y W%V %w", "%Y %U %w", "%Y %W", "%", "%-", "%%", "%H:%M:%S.%L", "%f", "%p %I", "\0", "%\0", "%j %q", string.Empty];
        string[] texts =
        [
            string.Empty, "\0", "\ud83d\ude00", "\ud83d", "2024-03-05", "+99:99", "Z", "99999999999999999999", new string('9', 256), new string(' ', 256),
            "-", "+", "12/31/9999, 11:59:59 PM", "9999 W53 9", "0 W1 0", "\u00a0\u00a0\u00a09", "x+9999", "+9999", "%", new string('%', 200),
        ];
        var calendars = new[] { TimeCalendar.Utc, TimeTestSupport.Calendar(TimeTestSupport.UsLike()), TimeTestSupport.Calendar(TimeTestSupport.Fixed(14)) };

        // Act / Assert: every combination returns, and the flag agrees with the number
        foreach (var calendar in calendars)
        {
            foreach (var specifier in specifiers)
            {
                foreach (var text in texts)
                {
                    var ok = TimeFormat.TryParse(specifier, text, calendar, out var ms);
                    Assert.Equal(ok, !double.IsNaN(ms));
                    Assert.True(!ok || TimeCalendar.IsValid(ms));
                }
            }
        }
    }

    /// <summary>
    ///     Validates that the parser ignores the current culture: names are matched as ASCII (the Turkish dotless i does not
    ///     break "FRIDAY" or "APRIL") and the digits are ASCII.
    /// </summary>
    [Fact]
    public void TimeFormatParse_IgnoresTheCurrentCulture()
    {
        // Arrange: names that contain the letter i
        static string ParseBoth() =>
            ((long)Parse("%A, %B %d, %Y", "FRIDAY, APRIL 5, 2024")).ToString(System.Globalization.CultureInfo.InvariantCulture)
            + "|" + ((long)Parse("%b %e %Y %p", "apr  5 2024 pm")).ToString(System.Globalization.CultureInfo.InvariantCulture);

        // Act: run under the invariant, Turkish and German cultures
        var invariant = ExpressionTestSupport.UnderCulture(string.Empty, ParseBoth);
        var turkish = ExpressionTestSupport.UnderCulture("tr-TR", ParseBoth);
        var german = ExpressionTestSupport.UnderCulture("de-DE", ParseBoth);

        // Assert: Friday 2024-04-05 is day 19,818 (1,712,275,200,000 ms), and the second text is noon of that day
        Assert.Equal("1712275200000|1712318400000", invariant);
        Assert.Equal(invariant, turkish);
        Assert.Equal(invariant, german);
    }
}
