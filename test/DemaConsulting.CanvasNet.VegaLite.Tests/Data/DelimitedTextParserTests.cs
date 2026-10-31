using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Data;

/// <summary>
///     Unit tests for <see cref="DelimitedTextParser"/> and <see cref="ParseDirectives"/>: the CSV, TSV and DSV grammar,
///     the shaping of ragged rows, per-column type inference, explicit conversions, ISO 8601 dates and the limits.
/// </summary>
public class DelimitedTextParserTests
{
    /// <summary>The location the tests pass as the location of the data.</summary>
    private static readonly VegaLiteLocation Where = new("spec.vl.json", "/data/url", 3, 9);

    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Parses delimited text with the test defaults.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="delimiter">The delimiter.</param>
    /// <param name="directives">The conversions; none by default, so the cells stay text.</param>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <param name="budget">The row budget.</param>
    /// <returns>The result.</returns>
    private static DelimitedResult Parse(
        string text,
        char delimiter = ',',
        ParseDirectives? directives = null,
        VegaLiteLimits? limits = null,
        long budget = 1_000_000) =>
        DelimitedTextParser.Parse(
            text,
            new DelimitedOptions(delimiter, directives ?? ParseDirectives.None, limits ?? VegaLiteLimits.Default, budget, "t.csv", Where, Ct));

    /// <summary>
    ///     Parses the <c>format</c> object of a <c>data</c> property written as JSON into directives.
    /// </summary>
    /// <param name="formatJson">The JSON of the <c>format</c> object.</param>
    /// <param name="defaultAuto">Whether an absent <c>parse</c> means inference (never in the loader; Vega infers only for <c>"auto"</c>).</param>
    /// <returns>The directives.</returns>
    private static ParseDirectives Directives(string formatJson, bool defaultAuto = false) =>
        ParseDirectives.Read(SpecParser.Parse("{\"data\":{\"url\":\"x.csv\",\"format\":" + formatJson + "}}").Root["data"]!["format"], defaultAuto);

    /// <summary>
    ///     Gets the milliseconds since the epoch of a UTC instant.
    /// </summary>
    /// <param name="year">The year.</param>
    /// <param name="month">The month.</param>
    /// <param name="day">The day.</param>
    /// <param name="hour">The hour.</param>
    /// <param name="minute">The minute.</param>
    /// <param name="second">The second.</param>
    /// <param name="millisecond">The millisecond.</param>
    /// <returns>The milliseconds.</returns>
    private static double Ms(int year, int month, int day, int hour = 0, int minute = 0, int second = 0, int millisecond = 0) =>
        new DateTimeOffset(year, month, day, hour, minute, second, millisecond, TimeSpan.Zero).ToUnixTimeMilliseconds();

    /// <summary>
    ///     Validates that a quoted field may hold delimiters, line breaks and doubled quotes, that an empty quoted field is
    ///     an empty text, and that a quote inside an unquoted field is an ordinary character.
    /// </summary>
    [Fact]
    public void DelimitedTextParser_Parse_CsvQuotesAndDoubledQuotes()
    {
        // Arrange: a comma and a line break inside quotes, doubled quotes, an empty quoted field, a stray quote
        const string text = "a,b,c\n\"x,1\",\"he said \"\"hi\"\"\",\"line1\nline2\"\n\"\",say \"x\",\"\"\"\"\n";

        // Act
        var result = Parse(text);

        // Assert
        Assert.Equal(2, result.Records.Count);
        Assert.Equal("x,1", result.Records[0]["a"].Text);
        Assert.Equal("he said \"hi\"", result.Records[0]["b"].Text);
        Assert.Equal("line1\nline2", result.Records[0]["c"].Text);
        Assert.Equal(string.Empty, result.Records[1]["a"].Text);
        Assert.Equal("say \"x\"", result.Records[1]["b"].Text);
        Assert.Equal("\"", result.Records[1]["c"].Text);
        Assert.All(result.Records.SelectMany(r => r.Values), v => Assert.True(v.IsString));
    }

    /// <summary>
    ///     Validates that a quote that is never closed and text after a closing quote are located failures.
    /// </summary>
    /// <param name="text">The malformed text.</param>
    /// <param name="expected">Part of the expected message.</param>
    [Theory]
    [InlineData("a,b\n\"never closed,1\n", "is not closed")]
    [InlineData("a,b\n\"x\"y,1\n", "unexpected text after the closing quote")]
    [InlineData("a,b\n1,\"x\" \n", "unexpected text after the closing quote")]
    public void DelimitedTextParser_Parse_MalformedQuotingIsLocatedFailure(string text, string expected)
    {
        // Arrange / Act
        var ex = Assert.Throws<VegaLiteRenderException>(() => Parse(text));

        // Assert
        Assert.Contains(expected, ex.Message, StringComparison.Ordinal);
        Assert.Contains("'t.csv'", ex.Message, StringComparison.Ordinal);
        Assert.Equal(Where, ex.Location);
    }

    /// <summary>
    ///     Validates that each format splits at its own delimiter and leaves the other characters alone, and that an
    ///     invalid delimiter is refused.
    /// </summary>
    [Fact]
    public void DelimitedTextParser_Parse_TsvAndDsvUseTheirDelimiters()
    {
        // Arrange: the same two columns written three ways, plus a comma inside tab separated text
        var tsv = Parse("a\tb\n1,5\t2\n", '\t');
        var semicolon = Parse("a;b\n1,5;2\n", ';');
        var pipe = Parse("a|b\n\"x|y\"|2\n", '|');
        var commaAsTab = Parse("a,b\n1,2\n", '\t');

        // Assert: delimiter characters other than the chosen one are ordinary text
        Assert.Equal("1,5", tsv.Records[0]["a"].Text);
        Assert.Equal("2", tsv.Records[0]["b"].Text);
        Assert.Equal("1,5", semicolon.Records[0]["a"].Text);
        Assert.Equal("x|y", pipe.Records[0]["a"].Text);
        Assert.Equal(["a,b"], commaAsTab.Records[0].Keys);

        // Assert: a quote, a line break or half of a surrogate pair cannot separate fields
        foreach (var bad in new[] { '"', '\n', '\r', '\uD83D' })
        {
            Assert.False(DelimitedTextParser.IsValidDelimiter(bad));
            Assert.Throws<VegaLiteRenderException>(() => Parse("a,b\n1,2\n", bad));
        }

        Assert.True(DelimitedTextParser.IsValidDelimiter('\t'));
        Assert.True(DelimitedTextParser.IsValidDelimiter(';'));
    }

    /// <summary>
    ///     Validates that a byte-order mark, line feeds, carriage return and line feed pairs, lone carriage returns, empty
    ///     lines and a missing final line break are all accepted.
    /// </summary>
    [Fact]
    public void DelimitedTextParser_Parse_BomCrLfAndLfAreAccepted()
    {
        // Arrange: a BOM, CRLF, LF, a lone CR, empty lines in between and no line break at the end
        const string text = "\uFEFFa,b\r\n1,2\r\n\r\n3,4\n\n5,6\r7,8";

        // Act
        var result = Parse(text);

        // Assert: the BOM is not part of the first column name and every line is a row
        Assert.Equal(["a", "b"], result.Records[0].Keys);
        Assert.Equal(["1", "3", "5", "7"], result.Records.Select(r => r["a"].Text));
        Assert.Equal(["2", "4", "6", "8"], result.Records.Select(r => r["b"].Text));
    }

    /// <summary>
    ///     Validates that a short row has null in the missing fields, entries beyond the header are ignored, and that
    ///     empty text and a header without rows give no rows.
    /// </summary>
    [Fact]
    public void DelimitedTextParser_Parse_RaggedRowsFillMissingAndIgnoreExtra()
    {
        // Arrange: a short row, a long row and a row of empty cells
        var result = Parse("a,b,c\n1\n1,2,3,4,5\n,,\n");

        // Assert: missing cells are null (not empty text), extra cells are dropped, empty cells are empty text
        Assert.Equal(3, result.Records.Count);
        Assert.Equal("1", result.Records[0]["a"].Text);
        Assert.True(result.Records[0]["b"].IsNull);
        Assert.True(result.Records[0]["c"].IsNull);
        Assert.Equal(["a", "b", "c"], result.Records[1].Keys);
        Assert.Equal("3", result.Records[1]["c"].Text);
        Assert.Equal(string.Empty, result.Records[2]["b"].Text);

        // Assert: nothing to read
        Assert.Empty(Parse(string.Empty).Records);
        Assert.Empty(Parse("a,b\n").Records);
        Assert.Empty(Parse("\n\n").Records);
    }

    /// <summary>
    ///     Validates the inference per column: booleans, numbers, ISO dates and times, and text, with the mixed columns,
    ///     the number spellings that are not numbers and the empty cells.
    /// </summary>
    [Fact]
    public void DelimitedTextParser_Parse_AutoInfersBooleanNumberIsoDateAndString()
    {
        // Arrange: one column per type, columns of mixed content, and number-like text that is not a number
        const string text = """
            flag,num,day,stamp,word,mixed,hex,special,blank,sci
            true,1,2024-03-05,2024-03-05T10:20:30Z,abc,1,0x10,NaN,,1e3
            FALSE,-2.5,2024-12-31,2024-03-05T10:20:30.5+02:00,def,x,5,Infinity,,.5
            ,007,,2024-03-05T10:20,ghi,2024-01-01,7,-Infinity,,+4
            """;

        // Act
        var records = Parse(text, directives: ParseDirectives.Auto).Records;

        // Assert: boolean column, with the empty cell null
        Assert.Equal([DataValueKind.Boolean, DataValueKind.Boolean, DataValueKind.Null], records.Select(r => r["flag"].Kind));
        Assert.True(records[0]["flag"].Boolean);
        Assert.False(records[1]["flag"].Boolean);

        // Assert: number column (integers, decimals, leading zeros, exponents, signs)
        Assert.Equal([1.0, -2.5, 7.0], records.Select(r => r["num"].Number));
        Assert.Equal([1000.0, 0.5, 4.0], records.Select(r => r["sci"].Number));

        // Assert: date column and date-time column, in UTC, with a zone offset applied
        Assert.Equal([Ms(2024, 3, 5), Ms(2024, 12, 31)], new[] { records[0]["day"].Milliseconds, records[1]["day"].Milliseconds });
        Assert.True(records[2]["day"].IsNull);
        Assert.Equal(Ms(2024, 3, 5, 10, 20, 30), records[0]["stamp"].Milliseconds);
        Assert.Equal(Ms(2024, 3, 5, 8, 20, 30, 500), records[1]["stamp"].Milliseconds);
        Assert.Equal(Ms(2024, 3, 5, 10, 20), records[2]["stamp"].Milliseconds);
        Assert.All(records, r => Assert.Equal(DataValueKind.Date, r["stamp"].Kind));

        // Assert: text, mixed columns, hexadecimal, NaN and infinities stay text; an empty column keeps its empty text
        Assert.All(new[] { "word", "mixed", "hex", "special" }, field => Assert.All(records, r => Assert.True(r[field].IsString, field)));
        Assert.Equal("0x10", records[0]["hex"].Text);
        Assert.Equal("NaN", records[0]["special"].Text);
        Assert.All(records, r => Assert.Equal(string.Empty, r["blank"].Text));
    }

    /// <summary>
    ///     Validates the date rules of <c>parse: "date"</c> and of the inference: the ECMAScript forms (a date is UTC, a
    ///     date-time without an offset is in the zone of the calendar, UTC here), the legacy forms of <c>Date.parse</c>, and
    ///     the texts that are not dates.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="valid">Whether it is a date.</param>
    [Theory]
    [InlineData("2024-03-05", true)]
    [InlineData(" 2024-03-05 ", true)]
    [InlineData("2024-03-05T10:20", true)]
    [InlineData("2024-03-05T10:20:30", true)]
    [InlineData("2024-03-05T10:20:30.123", true)]
    [InlineData("2024-03-05T10:20:30.1234567Z", true)]
    [InlineData("2024-03-05T10:20:30+05:30", true)]
    [InlineData("2024-03-05T10:20:30-08:00", true)]
    [InlineData("2024-02-29", true)]
    [InlineData("9999-12-31T23:59:59.999", true)]
    [InlineData("9999-12-31T23:59:59.9995", true)]
    [InlineData("2024-03-05 10:20:30", true)]
    [InlineData("March 5, 2024", true)]
    [InlineData("2024/03/05", true)]
    [InlineData("2024", true)]
    [InlineData("+010000-01-01T00:00:00Z", false)]
    [InlineData("2023-02-29", false)]
    [InlineData("2024-13-01", false)]
    [InlineData("24-03-05", false)]
    [InlineData("2024-03-05T25:00", false)]
    [InlineData("not a date", false)]
    [InlineData("", false)]
    public void DelimitedTextParser_Parse_DatesAreRecognizedByTheDateParseRules(string text, bool valid)
    {
        // Arrange / Act
        var result = ParseDirectives.TryParseDate(text, TimeCalendar.Utc, out var milliseconds);

        // Assert
        Assert.Equal(valid, result);
        if (valid)
        {
            Assert.True(milliseconds > 0);
        }
    }

    /// <summary>
    ///     Validates that text beyond the supported date range stays text under automatic inference: a column holding
    ///     one is not a date column, and nothing throws.
    /// </summary>
    [Fact]
    public void DelimitedTextParser_Parse_AutoOutOfRangeDateStaysText()
    {
        // Arrange: a column whose second cell is past the supported range of year 9999, and a column in range
        const string text = "late,ok\n2024-03-05,9999-12-31T23:59:59.999\n+010000-01-01T00:00:00Z,2024-03-05";

        // Act
        var result = Parse(text, directives: ParseDirectives.Auto);

        // Assert: the column with the out-of-range cell is text; the other column is dates, the maximum being exact
        Assert.All(result.Records, r => Assert.True(r["late"].IsString));
        Assert.Equal("+010000-01-01T00:00:00Z", result.Records[1]["late"].Text);
        Assert.All(result.Records, r => Assert.Equal(DataValueKind.Date, r["ok"].Kind));
        Assert.Equal(253402300799999.0, result.Records[0]["ok"].Milliseconds);
        Assert.True(result.Failures.IsEmpty);
    }

    /// <summary>
    ///     Validates that an explicit date conversion of text beyond the supported range is the ordinary failure: the
    ///     value is null, one warning counts it, and no exception escapes.
    /// </summary>
    [Fact]
    public void DelimitedTextParser_Parse_ExplicitOutOfRangeDateBecomesNullAndWarning()
    {
        // Arrange
        const string text = "d\n2024-03-05\n+010000-01-01T00:00:00Z";

        // Act
        var result = Parse(text, directives: Directives("{\"parse\":{\"d\":\"date\"}}"));

        // Assert: the representable date converts, the other is null with a warning naming the first offender
        Assert.Equal(Ms(2024, 3, 5), result.Records[0]["d"].Milliseconds);
        Assert.True(result.Records[1]["d"].IsNull);
        var report = Assert.Single(result.Failures.ToReports(Where));
        Assert.Equal("vl-data-parse-failed", report.Id);
        Assert.Equal(FeatureReportKind.Warning, report.Kind);
        Assert.Contains("1 value(s) of the field 'd' could not be read as date and are null (the first is in row 2: '+010000-01-01T00:00:00Z')", report.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that <c>parse: null</c> keeps every cell as text, and that an explicit map converts only the named fields,
    ///     turns empty cells into null, and records each value that cannot be read as null plus one failure per field.
    /// </summary>
    [Fact]
    public void DelimitedTextParser_Parse_NullKeepsTextAndExplicitMapCoercesFields()
    {
        // Arrange
        const string text = "n,b,d,s,u\n12,true,2024-01-02,1,7\nabc,0,xx,2,8\n,yes,,3,9\n 4 ,FALSE,2024-01-03T00:00:00Z,4,10";

        // Act
        var none = Parse(text, directives: Directives("{\"parse\":null}"));
        var mapped = Parse(text, directives: Directives("{\"parse\":{\"n\":\"number\",\"b\":\"boolean\",\"d\":\"date\",\"s\":\"string\"}}"));

        // Assert: parse null leaves every cell as read
        Assert.All(none.Records.SelectMany(r => r.Values), v => Assert.True(v.IsString));
        Assert.True(none.Failures.IsEmpty);

        // Assert: the named fields are converted; null input, empty cells and failures are null; "string" keeps text
        Assert.Equal([12.0, 0.0, 0.0, 4.0], mapped.Records.Select(r => r["n"].Number));
        Assert.Equal([DataValueKind.Number, DataValueKind.Null, DataValueKind.Null, DataValueKind.Number], mapped.Records.Select(r => r["n"].Kind));
        Assert.Equal([DataValueKind.Boolean, DataValueKind.Boolean, DataValueKind.Null, DataValueKind.Boolean], mapped.Records.Select(r => r["b"].Kind));
        Assert.True(mapped.Records[0]["b"].Boolean);
        Assert.False(mapped.Records[1]["b"].Boolean);
        Assert.Equal(Ms(2024, 1, 2), mapped.Records[0]["d"].Milliseconds);
        Assert.True(mapped.Records[1]["d"].IsNull);
        Assert.Equal("3", mapped.Records[2]["s"].Text);
        Assert.Equal("7", mapped.Records[0]["u"].Text);

        // Assert: one report per field and type, with the count and the first offender, located at the data
        var reports = mapped.Failures.ToReports(Where).ToList();
        Assert.Equal(["vl-data-parse-failed", "vl-data-parse-failed", "vl-data-parse-failed"], reports.Select(r => r.Id));
        Assert.All(reports, r => Assert.Equal(FeatureReportKind.Warning, r.Kind));
        Assert.All(reports, r => Assert.Equal(Where, r.Location));
        Assert.Contains("1 value(s) of the field 'b' could not be read as boolean and are null (the first is in row 3: 'yes')", reports[0].Message, StringComparison.Ordinal);
        Assert.Contains("the field 'd'", reports[1].Message, StringComparison.Ordinal);
        Assert.Contains("1 value(s) of the field 'n' could not be read as number and are null (the first is in row 2: 'abc')", reports[2].Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the conversions of other value kinds (JSON values and dates) to each explicit type.
    /// </summary>
    [Fact]
    public void DelimitedTextParser_Parse_ExplicitConversionsOfOtherKindsFollowTheDocumentedRules()
    {
        // Arrange
        var date = DataValue.FromDate(Ms(2024, 1, 2));

        // Act / Assert: numbers
        Assert.True(ParseDirectives.TryConvert(DataValue.FromBoolean(true), ParseType.Number, out var one));
        Assert.Equal(1, one.Number);
        Assert.True(ParseDirectives.TryConvert(date, ParseType.Number, out var epoch));
        Assert.Equal(Ms(2024, 1, 2), epoch.Number);

        // Act / Assert: booleans
        Assert.True(ParseDirectives.TryConvert(DataValue.FromNumber(0), ParseType.Boolean, out var no));
        Assert.False(no.Boolean);
        Assert.False(ParseDirectives.TryConvert(date, ParseType.Boolean, out _));

        // Act / Assert: dates (a number is milliseconds since the epoch, a boolean is no date, out of range is no date)
        Assert.True(ParseDirectives.TryConvert(DataValue.FromNumber(86_400_000), ParseType.Date, out var day));
        Assert.Equal(DataValueKind.Date, day.Kind);
        Assert.Equal(86_400_000, day.Milliseconds);
        Assert.False(ParseDirectives.TryConvert(DataValue.FromBoolean(true), ParseType.Date, out _));
        Assert.False(ParseDirectives.TryConvert(DataValue.FromNumber(1e300), ParseType.Date, out _));

        // Act / Assert: strings take the text of the value, and null stays null for every type
        Assert.True(ParseDirectives.TryConvert(DataValue.FromNumber(1.5), ParseType.String, out var text));
        Assert.Equal("1.5", text.Text);
        Assert.True(ParseDirectives.TryConvert(date, ParseType.String, out var iso));
        Assert.Equal("2024-01-02T00:00:00.000Z", iso.Text);
        foreach (var type in new[] { ParseType.Number, ParseType.Boolean, ParseType.Date, ParseType.String })
        {
            Assert.True(ParseDirectives.TryConvert(DataValue.Null, type, out var nothing));
            Assert.True(nothing.IsNull);
        }
    }

    /// <summary>
    ///     Validates that a custom date format is applied (it is no longer an unsupported feature): the field becomes a date
    ///     and no failure is recorded, next to a plain ISO column.
    /// </summary>
    [Fact]
    public void DelimitedTextParser_Parse_CustomDateFormatIsApplied()
    {
        // Arrange: a date column with a custom format next to a plain ISO column
        const string spec = """{"data":{"url":"x.csv","format":{"parse":{"d":"date:'%d/%m/%Y'","u":"utc:'%Y%m%d'","iso":"date"}}}}""";
        var tree = SpecParser.Parse(spec, "dates.json");
        var directives = ParseDirectives.Read(tree.Root["data"]!["format"], defaultAuto: false);

        // Act
        var result = Parse("d,u,iso\n05/03/2024,20240305,2024-03-05\n", directives: directives);

        // Assert: all three are the same UTC date, with no failure, and the catalog has nothing to report
        Assert.All(new[] { "d", "u", "iso" }, field => Assert.Equal(Ms(2024, 3, 5), result.Records[0][field].Milliseconds));
        Assert.All(new[] { "d", "u", "iso" }, field => Assert.Equal(DataValueKind.Date, result.Records[0][field].Kind));
        Assert.True(result.Failures.IsEmpty);
        Assert.Empty(FeatureCatalog.Visit(tree));
    }

    /// <summary>
    ///     Validates the limits: too many columns, a repeated column, a too long field, and more rows than the budget all fail
    ///     closed, exactly the limit is accepted, and the failures carry the location.
    /// </summary>
    [Fact]
    public void DelimitedTextParser_Parse_RowAndFieldLimitsFailClosed()
    {
        // Arrange / Act: more columns than MaxFieldCount, a repeated column name, an over-long unquoted and quoted field
        var columns = Assert.Throws<VegaLiteRenderException>(() => Parse("a,b,c\n1,2,3\n", limits: new VegaLiteLimits { MaxFieldCount = 2 }));
        var repeated = Assert.Throws<VegaLiteRenderException>(() => Parse("a,b,a\n1,2,3\n"));
        var unquoted = Assert.Throws<VegaLiteRenderException>(() => Parse("a\n123456\n", limits: new VegaLiteLimits { MaxStringLength = 5 }));
        var quoted = Assert.Throws<VegaLiteRenderException>(() => Parse("a\n\"123456\"\n", limits: new VegaLiteLimits { MaxStringLength = 5 }));
        var rows = Assert.Throws<VegaLiteRenderException>(() => Parse("a\n1\n2\n3\n", budget: 2));

        // Assert: each names its limit in the form the Tool recognizes and carries the location
        Assert.Contains("the limit of 2 (MaxFieldCount)", columns.Message, StringComparison.Ordinal);
        Assert.Contains("'a' more than once", repeated.Message, StringComparison.Ordinal);
        Assert.Contains("the limit of 5 characters (MaxStringLength)", unquoted.Message, StringComparison.Ordinal);
        Assert.Contains("MaxStringLength", quoted.Message, StringComparison.Ordinal);
        Assert.Contains("the limit of 1000000 rows in total (MaxDataRows)", rows.Message, StringComparison.Ordinal);
        Assert.All(new[] { columns, repeated, unquoted, quoted, rows }, e => Assert.Equal(Where, e.Location));

        // Assert: exactly the limits are accepted
        Assert.Equal(2, Parse("a,b\n1,2\n", limits: new VegaLiteLimits { MaxFieldCount = 2 }).Records[0].Count);
        Assert.Single(Parse("a\n12345\n", limits: new VegaLiteLimits { MaxStringLength = 5 }).Records);
        Assert.Equal(2, Parse("a\n1\n2\n", budget: 2).Records.Count);
    }

    /// <summary>
    ///     Validates that a header name with a dot is stored as an escaped canonical field name, so that the table finds it
    ///     under the path that addresses it.
    /// </summary>
    [Fact]
    public void DelimitedTextParser_Parse_HeaderNamesAreCanonicalFieldNames()
    {
        // Arrange / Act
        var result = Parse("a.b,c\n1,2\n");
        var table = DataTable.FromRecords(result.Records);

        // Assert: the dot needs the escape to address the column, as for a JSON key with a dot
        Assert.Equal(["a\\.b", "c"], table.Fields);
        Assert.Equal("1", table.GetValue(0, "a\\.b").Text);
        Assert.True(table.GetValue(0, "a.b").IsNull);
    }

    /// <summary>
    ///     Validates that parsing stops when the cancellation token is canceled.
    /// </summary>
    [Fact]
    public void DelimitedTextParser_Parse_CanceledTokenStopsParsing()
    {
        // Arrange: enough rows to reach a cancellation check
        var text = "a\n" + string.Join("\n", Enumerable.Range(0, 3000));
        using var source = new CancellationTokenSource();
        source.Cancel();

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => DelimitedTextParser.Parse(
            text,
            new DelimitedOptions(',', ParseDirectives.None, VegaLiteLimits.Default, 1_000_000, "t.csv", Where, source.Token)));
    }

    /// <summary>
    ///     Validates how the directives are read from a format: an absent <c>parse</c> converts nothing (as in Vega, for
    ///     delimited text and JSON alike), <c>"auto"</c> is the explicit opt-in to inference, null converts nothing, and an
    ///     object converts the named fields only.
    /// </summary>
    [Fact]
    public void DelimitedTextParser_Directives_ReadTheModeFromTheFormat()
    {
        // Arrange / Act
        var absentDelimited = ParseDirectives.Read(null, defaultAuto: false);
        var absentWithFormat = Directives("{\"delimiter\":\";\"}");
        var auto = Directives("{\"parse\":\"auto\"}");
        var none = Directives("{\"parse\":null}");
        var fields = Directives("{\"parse\":{\"a\":\"number\",\"b.c\":\"date\",\"d\":\"date:'%Y'\"}}");

        // Assert
        Assert.Equal(ParseMode.None, absentDelimited.Mode);
        Assert.Equal(ParseMode.None, absentWithFormat.Mode);
        Assert.Equal(ParseMode.Auto, auto.Mode);
        Assert.Equal(ParseMode.None, none.Mode);
        Assert.Equal(ParseMode.Fields, fields.Mode);
        Assert.Equal(new ParseSpec(ParseType.Number), fields.Fields["a"]);
        Assert.Equal(new ParseSpec(ParseType.Date), fields.Fields["b.c"]);
        Assert.Equal(new ParseSpec(ParseType.Date, "%Y", Utc: false), fields.Fields["d"]);
        Assert.Equal(new ParseSpec(ParseType.Date, "%Y%m", Utc: true), Directives("{\"parse\":{\"e\":\"utc:\\\"%Y%m\\\"\"}}").Fields["e"]);
    }

    /// <summary>
    ///     Validates that parsing with the default directives (absent <c>parse</c>) keeps leading zeros, ISO-looking dates
    ///     and booleans as text, while an explicit field map converts only the named field.
    /// </summary>
    [Fact]
    public void DelimitedTextParser_Parse_DefaultDelimitedTextKeepsLeadingZerosAndDateLikeText()
    {
        // Arrange: text that inference would convert
        const string text = "zip,day,flag,n\n01234,2024-03-05,true,7\n007,2024-03-06T10:00:00Z,false,8\n";
        var defaults = Directives("{\"delimiter\":\",\"}");
        var named = Directives("{\"parse\":{\"n\":\"number\"}}");

        // Act
        var byDefault = Parse(text, directives: defaults).Records;
        var byName = Parse(text, directives: named).Records;

        // Assert: nothing is converted by default
        Assert.All(byDefault, r => Assert.All(r.Values, v => Assert.Equal(DataValueKind.String, v.Kind)));
        Assert.Equal("01234", byDefault[0]["zip"].Text);
        Assert.Equal("007", byDefault[1]["zip"].Text);
        Assert.Equal("2024-03-05", byDefault[0]["day"].Text);
        Assert.Equal("true", byDefault[0]["flag"].Text);
        Assert.Equal("7", byDefault[0]["n"].Text);

        // Assert: a field map converts the named field only
        Assert.Equal(7, byName[0]["n"].Number);
        Assert.Equal("01234", byName[0]["zip"].Text);
        Assert.Equal(DataValueKind.String, byName[0]["day"].Kind);
    }

    /// <summary>
    ///     Validates that a header with more entries than <c>MaxFieldCount</c> is refused as soon as it exceeds the limit,
    ///     without reading (or materializing) the entries that follow, even when they are malformed or enormous.
    /// </summary>
    [Fact]
    public void DelimitedTextParser_Parse_HeaderFieldLimitStopsMaterialization()
    {
        // Arrange: a header of 100,000 entries against a limit of 3, followed by an unclosed quote that would also be an
        // error if the reader got that far
        var header = string.Join(',', Enumerable.Repeat("h", 100_000));
        var limits = new VegaLiteLimits { MaxFieldCount = 3 };

        // Act
        var ex = Assert.Throws<VegaLiteRenderException>(() => Parse(header + ",\"unclosed\n1\n", limits: limits));

        // Assert: the column limit is what is reported (not the later quote error), at the data location
        Assert.Contains("the limit of 3 (MaxFieldCount)", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("not closed", ex.Message, StringComparison.Ordinal);
        Assert.Equal(Where, ex.Location);
    }

    /// <summary>
    ///     Validates that entries beyond the header width are not kept but are still fully validated: a valid extra entry
    ///     is ignored, while an unclosed quote, text after a closing quote and an over-long entry past the header width
    ///     are located failures.
    /// </summary>
    [Fact]
    public void DelimitedTextParser_Parse_ExtraFieldsAreValidatedWithoutMaterialization()
    {
        // Arrange: many valid extra entries, and three malformed rows whose problem lies beyond the header width
        var many = string.Join(',', Enumerable.Repeat("x", 50_000));
        var limits = new VegaLiteLimits { MaxStringLength = 5 };

        // Act
        var valid = Parse("a,b\n1,2," + many + "\n3,4\n");
        var unclosed = Assert.Throws<VegaLiteRenderException>(() => Parse("a,b\n1,2,\"open\n3,4\n"));
        var afterQuote = Assert.Throws<VegaLiteRenderException>(() => Parse("a,b\n1,2,\"q\"x\n"));
        var tooLong = Assert.Throws<VegaLiteRenderException>(() => Parse("a,b\n1,2,123456\n", limits: limits));
        var tooLongQuoted = Assert.Throws<VegaLiteRenderException>(() => Parse("a,b\n1,2,\"123456\"\n", limits: limits));

        // Assert: the extras are dropped and the stored row has only the header fields
        Assert.Equal(2, valid.Records.Count);
        Assert.Equal(["a", "b"], valid.Records[0].Keys);
        Assert.Equal("2", valid.Records[0]["b"].Text);
        Assert.Equal("3", valid.Records[1]["a"].Text);

        // Assert: the malformed extras are still failures at the data location
        Assert.Contains("not closed", unclosed.Message, StringComparison.Ordinal);
        Assert.Contains("closing quote", afterQuote.Message, StringComparison.Ordinal);
        Assert.Contains("MaxStringLength", tooLong.Message, StringComparison.Ordinal);
        Assert.Contains("MaxStringLength", tooLongQuoted.Message, StringComparison.Ordinal);
        Assert.All(new[] { unclosed, afterQuote, tooLong, tooLongQuoted }, e => Assert.Equal(Where, e.Location));
    }
}
