using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Tests.Expressions;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Data;

/// <summary>
///     Unit tests of the date conversions of <see cref="ParseDirectives"/>: <c>parse: "date"</c> with the zone of the
///     calendar, the custom formats <c>date:'%fmt'</c> and <c>utc:'%fmt'</c>, the implicit date parse of temporal fields,
///     the date inference, and the limits and cancellation of the conversion. Every expected instant is worked out from
///     the definitions (see <see cref="TimeTestSupport"/>); no test reads the host's zone, clock or culture.
/// </summary>
public class ParseDirectivesDateTests
{
    /// <summary>The location the tests pass as the location of the data.</summary>
    private static readonly VegaLiteLocation Where = new("spec.vl.json", "/data", 1, 1);

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Parses the <c>format</c> object of a <c>data</c> property written as JSON into directives.
    /// </summary>
    /// <param name="formatJson">The JSON of the <c>format</c> object.</param>
    /// <param name="implicitFields">The implicit date fields, or none.</param>
    /// <returns>The directives.</returns>
    private static ParseDirectives Directives(string formatJson, params string[] implicitFields) =>
        ParseDirectives.Read(
            SpecParser.Parse("{\"data\":{\"url\":\"x.csv\",\"format\":" + formatJson + "}}").Root["data"]!["format"],
            defaultAuto: false,
            implicitFields.ToHashSet(StringComparer.Ordinal));

    /// <summary>
    ///     Builds rows of one text field.
    /// </summary>
    /// <param name="field">The field name.</param>
    /// <param name="texts">The texts.</param>
    /// <returns>The rows.</returns>
    private static List<Dictionary<string, DataValue>> Rows(string field, params string[] texts) =>
        [.. texts.Select(t => new Dictionary<string, DataValue>(StringComparer.Ordinal) { [field] = DataValue.FromString(t) })];

    /// <summary>
    ///     Applies directives to rows and returns the failures.
    /// </summary>
    /// <param name="directives">The directives.</param>
    /// <param name="rows">The rows, changed in place.</param>
    /// <param name="calendar">The calendar; UTC when null.</param>
    /// <returns>The failures.</returns>
    private static ParseFailures Apply(ParseDirectives directives, List<Dictionary<string, DataValue>> rows, TimeCalendar? calendar = null)
    {
        var failures = new ParseFailures();
        directives.Apply(rows, failures, Ct, calendar);
        return failures;
    }

    /// <summary>
    ///     Validates that <c>date</c> follows JavaScript's mixed rule in the zone of the calendar: a date-time without an
    ///     offset is local, a date alone and an explicit offset are not.
    /// </summary>
    [Fact]
    public void ParseDirectives_Date_UsesTheCalendarZoneForDateTimesWithoutOffset()
    {
        // Arrange: a zone five hours behind UTC in March (before the transition of 2024-03-10)
        var calendar = TimeTestSupport.Calendar(TimeTestSupport.UsLike());
        var rows = Rows("d", "2024-03-05T10:00", "2024-03-05", "2024-03-05T10:00Z", "2024-03-05T10:00+02:00");

        // Act
        var failures = Apply(Directives("{\"parse\":{\"d\":\"date\"}}"), rows, calendar);

        // Assert: 10:00 local is 15:00Z; the date alone is UTC midnight; offsets are honored
        Assert.Equal(
            [TimeTestSupport.Ms(2024, 3, 5, 15), TimeTestSupport.Ms(2024, 3, 5), TimeTestSupport.Ms(2024, 3, 5, 10), TimeTestSupport.Ms(2024, 3, 5, 8)],
            rows.Select(r => r["d"].Milliseconds));
        Assert.True(failures.IsEmpty);
    }

    /// <summary>
    ///     Validates that the default calendar is UTC, so that the zone never changes a specification that sets none.
    /// </summary>
    [Fact]
    public void ParseDirectives_Date_DefaultCalendarIsUtc()
    {
        // Arrange
        var rows = Rows("d", "2024-03-05T10:00");

        // Act
        Apply(Directives("{\"parse\":{\"d\":\"date\"}}"), rows);

        // Assert
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 5, 10), rows[0]["d"].Milliseconds);
    }

    /// <summary>
    ///     Validates that <c>date:'%fmt'</c> reads day-month-year text with a d3 time format.
    /// </summary>
    [Fact]
    public void ParseDirectives_CustomDate_ParsesDayMonthYear()
    {
        // Arrange
        var rows = Rows("d", "05/03/2024", "31/12/1999");

        // Act
        var failures = Apply(Directives("{\"parse\":{\"d\":\"date:'%d/%m/%Y'\"}}"), rows);

        // Assert
        Assert.Equal([TimeTestSupport.Ms(2024, 3, 5), TimeTestSupport.Ms(1999, 12, 31)], rows.Select(r => r["d"].Milliseconds));
        Assert.All(rows, r => Assert.Equal(DataValueKind.Date, r["d"].Kind));
        Assert.True(failures.IsEmpty);
    }

    /// <summary>
    ///     Validates the zone of the two custom directives: <c>date</c> reads local time, <c>utc</c> ignores the zone, and a
    ///     double-quoted format works like a single-quoted one.
    /// </summary>
    [Fact]
    public void ParseDirectives_CustomDate_Utc_IgnoresTheZone()
    {
        // Arrange: five hours behind UTC in March
        var calendar = TimeTestSupport.Calendar(TimeTestSupport.UsLike());
        var local = Rows("d", "05/03/2024 10");
        var utc = Rows("d", "05/03/2024 10");
        var quoted = Rows("d", "05/03/2024 10");

        // Act
        Apply(Directives("{\"parse\":{\"d\":\"date:'%d/%m/%Y %H'\"}}"), local, calendar);
        Apply(Directives("{\"parse\":{\"d\":\"utc:'%d/%m/%Y %H'\"}}"), utc, calendar);
        Apply(Directives("{\"parse\":{\"d\":\"utc:\\\"%d/%m/%Y %H\\\"\"}}"), quoted, calendar);

        // Assert
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 5, 15), local[0]["d"].Milliseconds);
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 5, 10), utc[0]["d"].Milliseconds);
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 5, 10), quoted[0]["d"].Milliseconds);
    }

    /// <summary>
    ///     Validates that a text that does not match the format is null and counted once per field, with the name of the
    ///     conversion in the warning.
    /// </summary>
    [Fact]
    public void ParseDirectives_CustomDate_FailureIsNullWithOneWarningPerField()
    {
        // Arrange
        var rows = Rows("d", "05/03/2024", "yesterday", "2024-03-05", string.Empty);

        // Act
        var failures = Apply(Directives("{\"parse\":{\"d\":\"date:'%d/%m/%Y'\"}}"), rows);

        // Assert: the good value converts, the two bad texts are null, the empty text is null and no failure
        Assert.Equal([DataValueKind.Date, DataValueKind.Null, DataValueKind.Null, DataValueKind.Null], rows.Select(r => r["d"].Kind));
        var report = Assert.Single(failures.ToReports(Where));
        Assert.Equal("vl-data-parse-failed", report.Id);
        Assert.Equal(FeatureReportKind.Warning, report.Kind);
        Assert.Contains("2 value(s) of the field 'd' could not be read as date with a custom format and are null (the first is in row 2: 'yesterday')", report.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a number is parsed through its JavaScript text (d3 coerces its input to a string), a date passes
    ///     through, and a boolean fails.
    /// </summary>
    [Fact]
    public void ParseDirectives_CustomDate_NumberIsParsedThroughItsText()
    {
        // Arrange: a JSON number, an integral double, a date and a boolean
        var rows = new List<Dictionary<string, DataValue>>
        {
            new(StringComparer.Ordinal) { ["d"] = DataValue.FromNumber(2020) },
            new(StringComparer.Ordinal) { ["d"] = DataValue.FromNumber(2020.5) },
            new(StringComparer.Ordinal) { ["d"] = DataValue.FromDate(TimeTestSupport.Ms(2024, 3, 5)) },
            new(StringComparer.Ordinal) { ["d"] = DataValue.FromBoolean(true) },
        };

        // Act
        var failures = Apply(Directives("{\"parse\":{\"d\":\"date:'%Y'\"}}"), rows);

        // Assert: "2020" is the year 2020; "2020.5" does not match; the date is kept; the boolean is no date
        Assert.Equal(TimeTestSupport.Ms(2020, 1, 1), rows[0]["d"].Milliseconds);
        Assert.True(rows[1]["d"].IsNull);
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 5), rows[2]["d"].Milliseconds);
        Assert.True(rows[3]["d"].IsNull);
        Assert.Contains("2 value(s)", Assert.Single(failures.ToReports(Where)).Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the plain conversion of the other kinds: a number stays milliseconds, a date passes, a boolean fails.
    /// </summary>
    [Fact]
    public void ParseDirectives_Date_NumberIsMillisecondsAndBooleanFails()
    {
        // Arrange
        var rows = new List<Dictionary<string, DataValue>>
        {
            new(StringComparer.Ordinal) { ["d"] = DataValue.FromNumber(86_400_000) },
            new(StringComparer.Ordinal) { ["d"] = DataValue.FromBoolean(false) },
            new(StringComparer.Ordinal) { ["d"] = DataValue.Null },
        };

        // Act
        var failures = Apply(Directives("{\"parse\":{\"d\":\"date\"}}"), rows);

        // Assert
        Assert.Equal(86_400_000, rows[0]["d"].Milliseconds);
        Assert.True(rows[1]["d"].IsNull);
        Assert.True(rows[2]["d"].IsNull);
        Assert.Single(failures.ToReports(Where));
    }

    /// <summary>
    ///     Validates that the implicit date parse applies to the named temporal fields only.
    /// </summary>
    [Fact]
    public void ParseDirectives_Implicit_AppliesToTemporalFieldsOnly()
    {
        // Arrange: two text fields with the same content, only d is temporal
        var rows = new List<Dictionary<string, DataValue>>
        {
            new(StringComparer.Ordinal) { ["d"] = DataValue.FromString("2024-03-05"), ["e"] = DataValue.FromString("2024-03-05") },
        };

        // Act: no parse property at all
        var directives = Directives("{\"type\":\"csv\"}", "d");
        Apply(directives, rows);

        // Assert
        Assert.Equal(ParseMode.Fields, directives.Mode);
        Assert.Equal(DataValueKind.Date, rows[0]["d"].Kind);
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 5), rows[0]["d"].Milliseconds);
        Assert.Equal(DataValueKind.String, rows[0]["e"].Kind);
    }

    /// <summary>
    ///     Validates that an explicit entry of a field wins over the implicit date parse, and that the other implicit
    ///     fields are still parsed.
    /// </summary>
    [Fact]
    public void ParseDirectives_Implicit_ExplicitParseWins()
    {
        // Arrange
        var rows = new List<Dictionary<string, DataValue>>
        {
            new(StringComparer.Ordinal) { ["d"] = DataValue.FromString("2024-03-05"), ["e"] = DataValue.FromString("2024-03-05"), ["f"] = DataValue.FromString("05/03/2024") },
        };

        // Act
        var directives = Directives("{\"parse\":{\"d\":\"string\",\"f\":\"date:'%d/%m/%Y'\"}}", "d", "e", "f");
        Apply(directives, rows);

        // Assert: d stays text, e is implicit, f uses its own format
        Assert.Equal(DataValueKind.String, rows[0]["d"].Kind);
        Assert.Equal(DataValueKind.Date, rows[0]["e"].Kind);
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 5), rows[0]["f"].Milliseconds);
    }

    /// <summary>
    ///     Validates that <c>parse: null</c> switches the implicit date parse off, and that it is told from an absent <c>parse</c>.
    /// </summary>
    [Fact]
    public void ParseDirectives_Implicit_ParseNullSuppresses()
    {
        // Arrange
        var rows = Rows("d", "2024-03-05");

        // Act
        var suppressed = Directives("{\"parse\":null}", "d");
        var absent = Directives("{}", "d");
        Apply(suppressed, rows);

        // Assert
        Assert.True(suppressed.Suppressed);
        Assert.False(absent.Suppressed);
        Assert.Equal(ParseMode.None, suppressed.Mode);
        Assert.Equal(DataValueKind.String, rows[0]["d"].Kind);
    }

    /// <summary>
    ///     Validates that an implicit field whose text is no date is null with the usual warning.
    /// </summary>
    [Fact]
    public void ParseDirectives_Implicit_UnparsableTextIsNullWithTheParseWarning()
    {
        // Arrange
        var rows = Rows("d", "2024-03-05", "n/a");

        // Act
        var failures = Apply(Directives("{}", "d"), rows);

        // Assert
        Assert.Equal(DataValueKind.Date, rows[0]["d"].Kind);
        Assert.True(rows[1]["d"].IsNull);
        Assert.Contains("could not be read as date and are null (the first is in row 2: 'n/a')", Assert.Single(failures.ToReports(Where)).Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that with <c>parse: "auto"</c> an implicit field that inference left as text is date-parsed, while
    ///     fields that inference converted are not touched again.
    /// </summary>
    [Fact]
    public void ParseDirectives_Implicit_AutoParsesTheTextThatInferenceLeaves()
    {
        // Arrange: d is mixed (no inference), e is a number column that is also temporal
        var rows = new List<Dictionary<string, DataValue>>
        {
            new(StringComparer.Ordinal) { ["d"] = DataValue.FromString("2024-03-05"), ["e"] = DataValue.FromString("1000") },
            new(StringComparer.Ordinal) { ["d"] = DataValue.FromString("oops"), ["e"] = DataValue.FromString("2000") },
        };

        // Act
        var failures = Apply(Directives("{\"parse\":\"auto\"}", "d", "e"), rows);

        // Assert: d is parsed with one failure; e was inferred as a number and stays a number
        Assert.Equal(DataValueKind.Date, rows[0]["d"].Kind);
        Assert.True(rows[1]["d"].IsNull);
        Assert.Equal([1000.0, 2000.0], rows.Select(r => r["e"].Number));
        Assert.Single(failures.ToReports(Where));
    }

    /// <summary>
    ///     Validates that automatic inference reads the legacy forms of <c>Date.parse</c> as well as ISO text.
    /// </summary>
    [Fact]
    public void ParseDirectives_Auto_InfersLegacyDatesAndIso()
    {
        // Arrange: one legacy column and one ISO column, and one column that mixes them (all are dates)
        var rows = new List<Dictionary<string, DataValue>>
        {
            new(StringComparer.Ordinal) { ["legacy"] = DataValue.FromString("Jan 2, 2006"), ["iso"] = DataValue.FromString("2006-01-02"), ["mixed"] = DataValue.FromString("Jan 2, 2006") },
            new(StringComparer.Ordinal) { ["legacy"] = DataValue.FromString("March 5, 2024"), ["iso"] = DataValue.FromString("2024-03-05T10:00:00Z"), ["mixed"] = DataValue.FromString("2024-03-05") },
        };

        // Act
        Apply(ParseDirectives.Auto, rows);

        // Assert
        Assert.Equal([TimeTestSupport.Ms(2006, 1, 2), TimeTestSupport.Ms(2024, 3, 5)], rows.Select(r => r["legacy"].Milliseconds));
        Assert.Equal([TimeTestSupport.Ms(2006, 1, 2), TimeTestSupport.Ms(2024, 3, 5, 10)], rows.Select(r => r["iso"].Milliseconds));
        Assert.Equal([TimeTestSupport.Ms(2006, 1, 2), TimeTestSupport.Ms(2024, 3, 5)], rows.Select(r => r["mixed"].Milliseconds));
    }

    /// <summary>
    ///     Validates the order of the inference: boolean, then number, then date, so that a year stays a number.
    /// </summary>
    [Fact]
    public void ParseDirectives_Auto_PrefersNumbersOverDates()
    {
        // Arrange
        var rows = Rows("y", "2024", "1999");

        // Act
        Apply(ParseDirectives.Auto, rows);

        // Assert
        Assert.Equal([DataValueKind.Number, DataValueKind.Number], rows.Select(r => r["y"].Kind));
        Assert.Equal([2024.0, 1999.0], rows.Select(r => r["y"].Number));
    }

    /// <summary>
    ///     Validates that inference reads a date-time without an offset in the zone of the calendar.
    /// </summary>
    [Fact]
    public void ParseDirectives_Auto_UsesTheCalendarZone()
    {
        // Arrange
        var rows = Rows("d", "2024-03-05T10:00");

        // Act
        Apply(ParseDirectives.Auto, rows, TimeTestSupport.Calendar(TimeTestSupport.Fixed(5, 30)));

        // Assert: 10:00 at +05:30 is 04:30Z
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 5, 4, 30), rows[0]["d"].Milliseconds);
    }

    /// <summary>
    ///     Validates that a cell of 1,048,576 characters is no date: the plain, the custom and the inferred conversions fail at once
    ///     and never copy or scan the cell beyond the parse cap.
    /// </summary>
    [Fact]
    public void ParseDirectives_Date_HugeCellIsNotADateAndCostsNothing()
    {
        // Arrange: 1,048,576 digits, as many letters, and a date padded with spaces
        var digits = new string('7', 1 << 20);
        var letters = new string('x', 1 << 20);
        var padded = new string(' ', 1 << 10) + "2024-03-05" + new string(' ', 1 << 10);

        // Act
        var plain = Rows("d", digits, letters, padded);
        var failures = Apply(Directives("{\"parse\":{\"d\":\"date\"}}"), plain);
        var custom = Rows("d", digits, letters);
        var customFailures = Apply(Directives("{\"parse\":{\"d\":\"date:'%Y'\"}}"), custom);
        var inferred = Rows("d", letters, letters);
        Apply(ParseDirectives.Auto, inferred);

        // Assert: only the padded date within the cap (after trimming) parses; the huge cells are null with a short message
        Assert.True(plain[0]["d"].IsNull);
        Assert.True(plain[1]["d"].IsNull);
        Assert.Equal(TimeTestSupport.Ms(2024, 3, 5), plain[2]["d"].Milliseconds);
        Assert.True(custom[0]["d"].IsNull);
        Assert.True(custom[1]["d"].IsNull);
        Assert.Equal(DataValueKind.String, inferred[0]["d"].Kind);
        Assert.All(failures.ToReports(Where).Concat(customFailures.ToReports(Where)), r => Assert.True(r.Message.Length < 300));
        Assert.False(ParseDirectives.TryParseDate(letters, TimeCalendar.Utc, out _));
    }

    /// <summary>
    ///     Validates that a canceled token stops the conversion of a large column.
    /// </summary>
    [Fact]
    public void ParseDirectives_Apply_PollsCancellationEveryBatch()
    {
        // Arrange: 10,000 rows and a token that is already canceled
        var rows = Rows("d", Enumerable.Repeat("2024-03-05", 10_000).ToArray());
        using var source = new CancellationTokenSource();
        source.Cancel();

        // Act / Assert: the explicit conversion and the inference both stop
        Assert.Throws<OperationCanceledException>(() =>
            Directives("{\"parse\":{\"d\":\"date\"}}").Apply(rows, new ParseFailures(), source.Token));
        Assert.Throws<OperationCanceledException>(() => ParseDirectives.Auto.Apply(rows, new ParseFailures(), source.Token));
    }

    /// <summary>
    ///     Validates that the rows themselves poll the token, not only the start of a field: the token is canceled while
    ///     the rows of a field are being read, and the conversion stops at the next batch boundary.
    /// </summary>
    /// <param name="auto">Whether the inference (true) or an explicit conversion (false) runs.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ParseDirectives_Apply_StopsInsideAColumnWhenCanceledMidway(bool auto)
    {
        // Arrange: a row list that cancels the token when row 5,000 is read
        using var source = new CancellationTokenSource();
        var rows = new CancelingRows(Rows("d", Enumerable.Repeat("2024-03-05", 10_000).ToArray()), 5_000, source);
        var directives = auto ? ParseDirectives.Auto : Directives("{\"parse\":{\"d\":\"date\"}}");

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => directives.Apply(rows, new ParseFailures(), source.Token));
        Assert.True(source.IsCancellationRequested);
    }

    /// <summary>
    ///     Validates that the date conversions ignore the host culture: month names match ASCII case-insensitively (a
    ///     Turkish upper-case I is no problem) and no digit or separator is culture specific.
    /// </summary>
    /// <param name="culture">The culture to run under.</param>
    [Theory]
    [InlineData("tr-TR")]
    [InlineData("de-DE")]
    public void ParseDirectives_Date_IgnoresTheCurrentCulture(string culture)
    {
        // Arrange / Act
        var result = ExpressionTestSupport.UnderCulture(culture, () =>
        {
            var rows = Rows("d", "APRIL 5, 2024", "05/04/2024", "2024-04-05T10:30:00.5Z");
            var custom = Rows("c", "APRIL 5, 2024", "april 5, 2024");
            Apply(Directives("{\"parse\":{\"d\":\"date\"}}"), rows);
            Apply(Directives("{\"parse\":{\"c\":\"date:'%B %d, %Y'\"}}"), custom);
            return (rows.Select(r => r["d"].Milliseconds).ToArray(), custom.Select(r => r["c"].Milliseconds).ToArray());
        });

        // Assert: the legacy form reads month/day/year, the custom format matches names ignoring case
        Assert.Equal(TimeTestSupport.Ms(2024, 4, 5), result.Item1[0]);
        Assert.Equal(TimeTestSupport.Ms(2024, 5, 4), result.Item1[1]);
        Assert.Equal(TimeTestSupport.Ms(2024, 4, 5, 10, 30, 0, 500), result.Item1[2]);
        Assert.Equal([TimeTestSupport.Ms(2024, 4, 5), TimeTestSupport.Ms(2024, 4, 5)], result.Item2);
    }

    /// <summary>
    ///     A row list that cancels a token when a row is read.
    /// </summary>
    /// <param name="inner">The rows.</param>
    /// <param name="cancelAt">The index whose read cancels.</param>
    /// <param name="source">The source to cancel.</param>
    private sealed class CancelingRows(List<Dictionary<string, DataValue>> inner, int cancelAt, CancellationTokenSource source)
        : IReadOnlyList<Dictionary<string, DataValue>>
    {
        /// <inheritdoc/>
        public int Count => inner.Count;

        /// <inheritdoc/>
        public Dictionary<string, DataValue> this[int index]
        {
            get
            {
                if (index == cancelAt)
                {
                    source.Cancel();
                }

                return inner[index];
            }
        }

        /// <inheritdoc/>
        public IEnumerator<Dictionary<string, DataValue>> GetEnumerator() => inner.GetEnumerator();

        /// <inheritdoc/>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
