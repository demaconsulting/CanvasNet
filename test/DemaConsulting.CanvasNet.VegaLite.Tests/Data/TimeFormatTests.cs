using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Tests.Expressions;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Data;

/// <summary>
///     Unit tests of the d3-time-format formatter of <see cref="TimeFormat"/>. The expected texts are worked out by hand from
///     the definition of each directive for 2024-03-05T13:04:09.007Z, which is 1,709,643,849,007 ms (day 19,787 is 2024-03-05, a
///     Tuesday and the 65th day of the year, plus 13:04:09.007 = 47,049,007 ms).
/// </summary>
public class TimeFormatTests
{
    /// <summary>The instant 2024-03-05T13:04:09.007Z, worked out by hand: 19,787 days * 86,400,000 + 47,049,007.</summary>
    private const double Instant = 1_709_643_849_007;

    /// <summary>The instant 2024-03-05T03:04:09.007Z: the same day with a single-digit hour.</summary>
    private const double EarlyInstant = 1_709_607_849_007;

    /// <summary>The limit used when a test does not care about it.</summary>
    private const int Limit = 1000;

    /// <summary>
    ///     Validates the text of every directive for the anchor instant in UTC.
    /// </summary>
    /// <param name="specifier">The specifier.</param>
    /// <param name="expected">The expected text.</param>
    [Theory]
    [InlineData("%a", "Tue")]
    [InlineData("%A", "Tuesday")]
    [InlineData("%b", "Mar")]
    [InlineData("%B", "March")]
    [InlineData("%c", "3/5/2024, 1:04:09 PM")]
    [InlineData("%d", "05")]
    [InlineData("%e", " 5")]
    [InlineData("%f", "007000")]
    [InlineData("%g", "24")]
    [InlineData("%G", "2024")]
    [InlineData("%H", "13")]
    [InlineData("%I", "01")]
    [InlineData("%j", "065")]
    [InlineData("%L", "007")]
    [InlineData("%m", "03")]
    [InlineData("%M", "04")]
    [InlineData("%p", "PM")]
    [InlineData("%q", "1")]
    [InlineData("%Q", "1709643849007")]
    [InlineData("%s", "1709643849")]
    [InlineData("%S", "09")]
    [InlineData("%u", "2")]
    [InlineData("%U", "09")]
    [InlineData("%V", "10")]
    [InlineData("%w", "2")]
    [InlineData("%W", "10")]
    [InlineData("%x", "3/5/2024")]
    [InlineData("%X", "1:04:09 PM")]
    [InlineData("%y", "24")]
    [InlineData("%Y", "2024")]
    [InlineData("%Z", "+0000")]
    [InlineData("%%", "%")]
    [InlineData("%Y-%m-%dT%H:%M:%S.%L", "2024-03-05T13:04:09.007")]
    [InlineData("plain text", "plain text")]
    [InlineData("", "")]
    public void TimeFormat_Format_EveryDirective_Utc(string specifier, string expected)
    {
        // Arrange: compile the specifier
        var formatter = TimeFormat.Compile(specifier);

        // Act: format the anchor instant in UTC
        var actual = formatter.Format(Instant, TimeCalendar.Utc, Limit);

        // Assert: the hand-derived text
        Assert.Equal(expected, actual);
    }

    /// <summary>
    ///     Validates the three padding flags: <c>-</c> removes the padding, <c>_</c> pads with spaces and <c>0</c> pads with zeros.
    /// </summary>
    /// <param name="specifier">The specifier.</param>
    /// <param name="expected">The expected text for 2024-03-05T03:04:09.007Z.</param>
    [Theory]
    [InlineData("%-d", "5")]
    [InlineData("%_d", " 5")]
    [InlineData("%0e", "05")]
    [InlineData("%-e", "5")]
    [InlineData("%-m", "3")]
    [InlineData("%_m", " 3")]
    [InlineData("%_H", " 3")]
    [InlineData("%-H", "3")]
    [InlineData("%-I", "3")]
    [InlineData("%_I", " 3")]
    [InlineData("%-j", "65")]
    [InlineData("%_j", " 65")]
    [InlineData("%-L", "7")]
    [InlineData("%-f", "7000")]
    [InlineData("%-M", "4")]
    [InlineData("%-S", "9")]
    [InlineData("%-U", "9")]
    [InlineData("%_Y", "2024")]
    [InlineData("%-y", "24")]
    [InlineData("%-a", "Tue")]
    [InlineData("%-Q", "1709607849007")]
    [InlineData("%-x", "3/5/2024")]
    public void TimeFormat_Format_PaddingFlags(string specifier, string expected)
    {
        // Arrange / Act: format the early-morning instant
        var actual = TimeFormat.Compile(specifier).Format(EarlyInstant, TimeCalendar.Utc, Limit);

        // Assert: the flag decides the fill
        Assert.Equal(expected, actual);
    }

    /// <summary>
    ///     Validates d3's <c>pad</c> function, including negative values, the absent fill and values wider than the width.
    /// </summary>
    [Fact]
    public void TimeFormat_Pad_NegativeWideAndNoFillValues()
    {
        // Arrange / Act / Assert: the sign comes first, then the fill, then the digits
        Assert.Equal("-05", TimeFormat.Pad(-5, '0', 2));
        Assert.Equal("-  123", TimeFormat.Pad(-123, ' ', 5));
        Assert.Equal("00", TimeFormat.Pad(0, '0', 2));
        Assert.Equal("1234", TimeFormat.Pad(1234, '0', 2));
        Assert.Equal("5", TimeFormat.Pad(5, '\0', 3));
        Assert.Equal("-9223372036854775808", TimeFormat.Pad(long.MinValue, '0', 2));
    }

    /// <summary>
    ///     Validates a twelve-hour clock: 00:00 is 12 AM, 12:00 is 12 PM, 23:00 is 11 PM.
    /// </summary>
    [Fact]
    public void TimeFormat_Format_TwelveHourClock_MidnightAndNoon()
    {
        // Arrange: three instants of 2024-03-05 at 00:00, 12:00 and 23:00 (the day starts at 1,709,596,800,000)
        var formatter = TimeFormat.Compile("%I %p");

        // Act: format them
        var midnight = formatter.Format(1_709_596_800_000, TimeCalendar.Utc, Limit);
        var noon = formatter.Format(1_709_596_800_000 + (12 * 3_600_000.0), TimeCalendar.Utc, Limit);
        var evening = formatter.Format(1_709_596_800_000 + (23 * 3_600_000.0), TimeCalendar.Utc, Limit);

        // Assert: hour 0 shows as 12
        Assert.Equal("12 AM", midnight);
        Assert.Equal("12 PM", noon);
        Assert.Equal("11 PM", evening);
    }

    /// <summary>
    ///     Validates the week numbers around the new year: Sunday-based (<c>%U</c>) and Monday-based (<c>%W</c>) weeks count from the
    ///     first Sunday or Monday, and 2012-12-31 is in week 53 of both.
    /// </summary>
    /// <param name="year">The year.</param>
    /// <param name="month">The month.</param>
    /// <param name="day">The day.</param>
    /// <param name="expected">The expected <c>%U %W</c>.</param>
    [Theory]
    [InlineData(2012, 1, 1, "01 00")]
    [InlineData(2012, 1, 2, "01 01")]
    [InlineData(2012, 12, 30, "53 52")]
    [InlineData(2012, 12, 31, "53 53")]
    [InlineData(2013, 1, 1, "00 00")]
    [InlineData(2013, 1, 6, "01 00")]
    [InlineData(2013, 1, 7, "01 01")]
    public void TimeFormat_Format_WeekNumbers_SundayAndMonday(int year, int month, int day, string expected)
    {
        // Arrange / Act: format %U and %W of the date
        var actual = TimeFormat.Compile("%U %W").Format(TimeTestSupport.Ms(year, month, day), TimeCalendar.Utc, Limit);

        // Assert: the hand-counted week numbers
        Assert.Equal(expected, actual);
    }

    /// <summary>
    ///     Validates the ISO 8601 week number and week-based year at the boundaries of years.
    /// </summary>
    /// <param name="year">The year.</param>
    /// <param name="month">The month.</param>
    /// <param name="day">The day.</param>
    /// <param name="expected">The expected <c>%V %G %g</c>.</param>
    [Theory]
    [InlineData(2021, 1, 3, "53 2020 20")]
    [InlineData(2020, 12, 31, "53 2020 20")]
    [InlineData(2024, 12, 30, "01 2025 25")]
    [InlineData(2018, 12, 31, "01 2019 19")]
    [InlineData(2015, 1, 1, "01 2015 15")]
    [InlineData(2016, 1, 3, "53 2015 15")]
    [InlineData(2024, 1, 1, "01 2024 24")]
    [InlineData(2024, 3, 5, "10 2024 24")]
    public void TimeFormat_Format_IsoWeek_BoundaryYears(int year, int month, int day, string expected)
    {
        // Arrange / Act: format the ISO week of the date
        var actual = TimeFormat.Compile("%V %G %g").Format(TimeTestSupport.Ms(year, month, day), TimeCalendar.Utc, Limit);

        // Assert: the week belongs to the year of its Thursday
        Assert.Equal(expected, actual);
    }

    /// <summary>
    ///     Validates the years at the ends of the range: they are padded to four digits and the year 0 of a western zone's wall time is shown.
    /// </summary>
    [Fact]
    public void TimeFormat_Format_ShortYearsAndTheYearZero()
    {
        // Arrange: year 5 and year 1 in UTC, and the first instant of year 1 in a zone five hours behind (wall time 0000-12-31 19:00)
        var yearFive = TimeFormat.Compile("%Y|%y|%C").Format(TimeTestSupport.Ms(5, 3, 5), TimeCalendar.Utc, Limit);
        var yearOne = TimeFormat.Compile("%Y-%m-%d").Format(TimeTestSupport.Ms(1, 1, 1), TimeCalendar.Utc, Limit);
        var behind = TimeFormat.Compile("%Y-%m-%d %H:%M").Format(TimeTestSupport.Ms(1, 1, 1), TimeTestSupport.Calendar(TimeTestSupport.Fixed(-5)), Limit);
        var last = TimeFormat.Compile("%Y-%m-%dT%H:%M:%S.%L").Format(253_402_300_799_999, TimeCalendar.Utc, Limit);

        // Assert: zero padded years; %C is not a d3 directive and is copied as its letter
        Assert.Equal("0005|05|C", yearFive);
        Assert.Equal("0001-01-01", yearOne);
        Assert.Equal("0000-12-31 19:00", behind);
        Assert.Equal("9999-12-31T23:59:59.999", last);
    }

    /// <summary>
    ///     Validates <c>%Z</c>: the offset of the instant as <c>±hhmm</c> in a half-hour zone and in a daylight saving zone.
    /// </summary>
    [Fact]
    public void TimeFormat_Format_Zone_Z_ShowsTheOffsetOfTheInstant()
    {
        // Arrange: a +05:30 zone, a -03:30 zone and a US-like zone
        var formatter = TimeFormat.Compile("%Z");
        var indian = TimeTestSupport.Calendar(TimeTestSupport.Fixed(5, 30));
        var newfoundland = TimeTestSupport.Calendar(TimeTestSupport.Fixed(-3, -30));
        var usLike = TimeTestSupport.Calendar(TimeTestSupport.UsLike());

        // Act: format instants in winter and summer
        var plus = formatter.Format(Instant, indian, Limit);
        var minus = formatter.Format(Instant, newfoundland, Limit);
        var winter = formatter.Format(TimeTestSupport.Ms(2024, 1, 15, 12), usLike, Limit);
        var summer = formatter.Format(TimeTestSupport.Ms(2024, 7, 15, 12), usLike, Limit);

        // Assert: the offsets of the instants
        Assert.Equal("+0530", plus);
        Assert.Equal("-0330", minus);
        Assert.Equal("-0500", winter);
        Assert.Equal("-0400", summer);
    }

    /// <summary>
    ///     Validates that the local fields follow the offset: 2024-03-05T20:00Z is 01:30 on the 6th in a +05:30 zone, and the
    ///     US-like zone changes its offset at the daylight saving start.
    /// </summary>
    [Fact]
    public void TimeFormat_Format_Zone_LocalFieldsFollowTheOffset()
    {
        // Arrange: formatters for date and time of day
        var formatter = TimeFormat.Compile("%Y-%m-%d %H:%M:%S.%L %a");
        var indian = TimeTestSupport.Calendar(TimeTestSupport.Fixed(5, 30));
        var usLike = TimeTestSupport.Calendar(TimeTestSupport.UsLike());

        // Act: format the instants (the US change is at 2024-03-10T07:00Z)
        var shifted = formatter.Format(TimeTestSupport.Ms(2024, 3, 5, 20), indian, Limit);
        var before = formatter.Format(1_710_054_000_000 - 1, usLike, Limit);
        var after = formatter.Format(1_710_054_000_000, usLike, Limit);

        // Assert: the wall time before the change is 01:59:59.999 and after it 03:00
        Assert.Equal("2024-03-06 01:30:00.000 Wed", shifted);
        Assert.Equal("2024-03-10 01:59:59.999 Sun", before);
        Assert.Equal("2024-03-10 03:00:00.000 Sun", after);
    }

    /// <summary>
    ///     Validates that <c>%Q</c> and <c>%s</c> are not shifted by the zone and floor a negative instant.
    /// </summary>
    [Fact]
    public void TimeFormat_Format_Timestamps_AreZoneIndependentAndFloorNegatives()
    {
        // Arrange: the formatters and a zone
        var zone = TimeTestSupport.Calendar(TimeTestSupport.Fixed(5, 30));

        // Act: format a positive and a negative instant
        var positive = TimeFormat.Compile("%Q %s").Format(Instant, zone, Limit);
        var negative = TimeFormat.Compile("%Q %s").Format(-1500, TimeCalendar.Utc, Limit);

        // Assert: the milliseconds, and the seconds rounded down (-1.5 s is -2 s)
        Assert.Equal("1709643849007 1709643849", positive);
        Assert.Equal("-1500 -2", negative);
    }

    /// <summary>
    ///     Validates the unknown-directive behavior of d3: the percent sign is dropped and the letter copied; a trailing
    ///     percent sign gives nothing.
    /// </summary>
    /// <param name="specifier">The specifier.</param>
    /// <param name="expected">The expected text.</param>
    [Theory]
    [InlineData("%k", "k")]
    [InlineData("%E", "E")]
    [InlineData("%5", "5")]
    [InlineData("100%", "100")]
    [InlineData("%-", "")]
    [InlineData("%0", "")]
    [InlineData("%-k", "k")]
    [InlineData("a%%b", "a%b")]
    [InlineData("%Y%", "2024")]
    [InlineData("%_%", "%")]
    public void TimeFormat_Format_UnknownDirective_FollowsD3(string specifier, string expected)
    {
        // Arrange / Act: format
        var actual = TimeFormat.Compile(specifier).Format(Instant, TimeCalendar.Utc, Limit);

        // Assert: the pinned d3 behavior
        Assert.Equal(expected, actual);
    }

    /// <summary>
    ///     Validates that a NaN, an infinite and an out-of-range instant format as <c>Invalid Date</c> for any specifier.
    /// </summary>
    [Fact]
    public void TimeFormat_Format_InvalidDateIsInvalidDateText()
    {
        // Arrange: a formatter and invalid instants
        var formatter = TimeFormat.Compile("%Y-%m-%d");
        double[] invalid = [double.NaN, double.PositiveInfinity, double.NegativeInfinity, DataValue.MinDateMilliseconds - 1, DataValue.MaxDateMilliseconds + 1, 1e300];

        // Act / Assert: all give the same text
        foreach (var value in invalid)
        {
            Assert.Equal("Invalid Date", formatter.Format(value, TimeCalendar.Utc, Limit));
        }
    }

    /// <summary>
    ///     Validates the output limit: text of exactly the limit is returned, one more character throws, and a huge literal
    ///     stops early.
    /// </summary>
    [Fact]
    public void TimeFormat_Format_TooLong_Throws()
    {
        // Arrange: %c is 20 characters here, so four of them are 80
        var four = TimeFormat.Compile("%c%c%c%c");
        var huge = TimeFormat.Compile(new string('x', 5000));

        // Act: format at the limit and beyond it
        var exact = four.Format(Instant, TimeCalendar.Utc, 80);
        var tooShort = Assert.Throws<TimeFormatTooLongException>(() => four.Format(Instant, TimeCalendar.Utc, 79));
        var hugeError = Assert.Throws<TimeFormatTooLongException>(() => huge.Format(Instant, TimeCalendar.Utc, 100));
        var invalidError = Assert.Throws<TimeFormatTooLongException>(() => four.Format(double.NaN, TimeCalendar.Utc, 5));

        // Assert: the exact fit works and the exceptions carry the limit
        Assert.Equal(80, exact.Length);
        Assert.Equal(79, tooShort.MaxLength);
        Assert.Equal(100, hugeError.MaxLength);
        Assert.Equal(5, invalidError.MaxLength);
        Assert.Equal(string.Empty, TimeFormat.Compile(string.Empty).Format(Instant, TimeCalendar.Utc, 0));
    }

    /// <summary>
    ///     Validates that a fraction of a millisecond is truncated and that the same formatter object is reusable on several calendars.
    /// </summary>
    [Fact]
    public void TimeFormat_Format_FractionIsTruncatedAndFormatterIsReusable()
    {
        // Arrange: one formatter
        var formatter = TimeFormat.Compile("%S.%L");

        // Act: format fractional instants and use two calendars
        var fraction = formatter.Format(Instant + 0.9, TimeCalendar.Utc, Limit);
        var negativeFraction = formatter.Format(-0.5, TimeCalendar.Utc, Limit);
        var zoned = formatter.Format(Instant, TimeTestSupport.Calendar(TimeTestSupport.Fixed(0, 30)), Limit);

        // Assert: 0.9 ms is dropped, -0.5 ms truncates toward zero (the epoch), and the half-hour zone keeps the seconds
        Assert.Equal("09.007", fraction);
        Assert.Equal("00.000", negativeFraction);
        Assert.Equal("09.007", zoned);
    }

    /// <summary>
    ///     Validates that the formatter ignores the current culture: names stay English, <c>I</c> is not turned into a
    ///     dotless letter, and the digits are ASCII.
    /// </summary>
    [Fact]
    public void TimeFormat_Format_IgnoresTheCurrentCulture()
    {
        // Arrange: a specifier with names, a period and numbers
        static string Format() =>
            TimeFormat.Compile("%A %B %a %b %p %I %d %Y %j %c").Format(Instant, TimeCalendar.Utc, Limit);

        // Act: run under the invariant, Turkish and German cultures
        var invariant = ExpressionTestSupport.UnderCulture(string.Empty, Format);
        var turkish = ExpressionTestSupport.UnderCulture("tr-TR", Format);
        var german = ExpressionTestSupport.UnderCulture("de-DE", Format);

        // Assert: English text and identical results
        Assert.Equal("Tuesday March Tue Mar PM 01 05 2024 065 3/5/2024, 1:04:09 PM", invariant);
        Assert.Equal(invariant, turkish);
        Assert.Equal(invariant, german);
    }
}
