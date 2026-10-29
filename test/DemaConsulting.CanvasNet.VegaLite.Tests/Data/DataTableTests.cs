using System.Globalization;
using DemaConsulting.CanvasNet.VegaLite.Data;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Data;

/// <summary>
///     Tests of the immutable data table, the scalar value model, JavaScript number formatting and field paths, with
///     expected values written out by hand from the JavaScript and Vega rules.
/// </summary>
public class DataTableTests
{
    /// <summary>
    ///     Validates that a table keeps typed values, orders its fields by code unit whatever order they were given in, copies
    ///     its input, and reads cells through typed accessors, positions and field paths.
    /// </summary>
    [Fact]
    public void DataTable_Create_StoresTypedImmutableRows()
    {
        // Arrange: two fields given in reverse ordinal order and two rows of mixed value kinds
        var fields = new List<string> { "b", "a" };
        var row1 = new List<DataValue> { DataValue.FromString("x"), DataValue.FromNumber(1.5) };
        var row2 = new List<DataValue> { DataValue.Null, DataValue.FromBoolean(true) };
        var rows = new List<IReadOnlyList<DataValue>> { row1, row2 };

        // Act
        var table = DataTable.Create(fields, rows);
        fields[0] = "changed";
        row1[0] = DataValue.FromString("mutated");

        // Assert: the table orders its fields by code unit and matches the values to the fields as they were given
        Assert.Equal(2, table.RowCount);
        Assert.Equal(2, table.FieldCount);
        Assert.Equal(["a", "b"], table.Fields);
        Assert.Equal(0, table.IndexOfField("a"));
        Assert.Equal(1, table.IndexOfField("b"));
        Assert.Equal(-1, table.IndexOfField("missing"));

        // Assert: typed accessors return a value only for the matching kind
        Assert.Equal(1.5, table.GetNumber(0, 0));
        Assert.Null(table.GetNumber(0, 1));
        Assert.Equal("x", table.GetString(0, 1));
        Assert.Null(table.GetString(0, 0));
        Assert.True(table.GetBoolean(1, 0));
        Assert.Null(table.GetBoolean(0, 0));
        Assert.True(table.GetValue(1, 1).IsNull);
        Assert.Equal(DataValueKind.Boolean, table.GetValue(1, "a").Kind);

        // Assert: the table copied its input, so later changes to it are not visible
        Assert.Equal("x", table.GetString(0, 1));
        Assert.Equal(["a", "b"], table.Fields);

        // Assert: a row is a read-only view in field order and a column is read row by row
        var row = table.GetRow(0);
        Assert.Equal(2, row.Length);
        Assert.Equal(1.5, row[0].Number);
        Assert.Equal(["x", "null"], table.Column(1).Select(v => v.ToText()).ToArray());
    }

    /// <summary>
    ///     Validates the date value: it is held as UTC milliseconds since the epoch, reads as that number where a number is
    ///     wanted, formats as ISO 8601 with the invariant culture, sorts between numbers and strings, and is never equal to
    ///     the number or the text it looks like.
    /// </summary>
    [Fact]
    public void DataTable_DateValuesHaveDeterministicQuantitativeAndTextForms()
    {
        // Arrange: 2024-03-05T00:00:00Z, the epoch, a time before the epoch, and the same instant from a local and a UTC DateTime
        var march = DataValue.FromDate(1_709_596_800_000);
        var epoch = DataValue.FromDate(0);
        var before = DataValue.FromDate(-86_400_500);
        var utc = DataValue.FromDateTime(new DateTime(2024, 3, 5, 0, 0, 0, DateTimeKind.Utc));
        var unspecified = DataValue.FromDateTime(new DateTime(2024, 3, 5, 0, 0, 0, DateTimeKind.Unspecified));
        var local = DataValue.FromDateTime(new DateTime(2024, 3, 5, 0, 0, 0, DateTimeKind.Utc).ToLocalTime());
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        string[] text;
        try
        {
            text = [march.ToText(), epoch.ToText(), before.ToText(), march.ToString()];
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }

        // Assert: the kind and the milliseconds, with no dependence on the time zone of the host
        Assert.Equal(DataValueKind.Date, march.Kind);
        Assert.True(march.IsDate);
        Assert.False(march.IsNumber);
        Assert.Equal(1_709_596_800_000, march.Milliseconds);
        Assert.Equal(0, march.Number);
        Assert.Equal(march, utc);
        Assert.Equal(march, unspecified);
        Assert.Equal(march, local);

        // Assert: the quantitative value is the number of milliseconds
        Assert.True(march.TryGetQuantitative(out var number));
        Assert.Equal(1_709_596_800_000, number);
        Assert.True(before.TryGetQuantitative(out var negative));
        Assert.Equal(-86_400_500, negative);

        // Assert: the text is the ISO 8601 form in UTC whatever the culture is
        Assert.Equal(["2024-03-05T00:00:00.000Z", "1970-01-01T00:00:00.000Z", "1969-12-30T23:59:59.500Z", "2024-03-05T00:00:00.000Z"], text);

        // Assert: a date is equal only to the same instant (not to the number or text of it), zero equals zero, and hashing agrees
        Assert.NotEqual(march, DataValue.FromNumber(1_709_596_800_000));
        Assert.NotEqual(march, DataValue.FromString("2024-03-05T00:00:00.000Z"));
        Assert.Equal(DataValue.FromDate(0), DataValue.FromDate(-0.0));
        Assert.Equal(march.GetHashCode(), utc.GetHashCode());
        Assert.Equal(epoch.GetHashCode(), DataValue.FromDate(-0.0).GetHashCode());

        // Assert: dates sort by instant, after numbers and before strings (the order of the kinds), and null sorts first
        Assert.True(DataValue.Compare(before, epoch) < 0);
        Assert.True(DataValue.Compare(march, epoch) > 0);
        Assert.Equal(0, DataValue.Compare(march, utc));
        Assert.True(DataValue.Compare(DataValue.FromNumber(1e300), epoch) < 0);
        Assert.True(DataValue.Compare(march, DataValue.FromString("a")) < 0);
        Assert.True(DataValue.Compare(DataValue.Null, epoch) < 0);

        // Assert: a date outside the years 1 to 9999, or not finite, is refused
        Assert.Throws<ArgumentOutOfRangeException>(() => DataValue.FromDate(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => DataValue.FromDate(double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => DataValue.FromDate(-62_135_596_800_001));
        Assert.Throws<ArgumentOutOfRangeException>(() => DataValue.FromDate(253_402_300_800_000));
        Assert.Equal("0001-01-01T00:00:00.000Z", DataValue.FromDate(-62_135_596_800_000).ToText());
        Assert.Equal("9999-12-31T23:59:59.999Z", DataValue.FromDate(253_402_300_799_999).ToText());
    }

    /// <summary>
    ///     Validates that a table can be built from per-row dictionaries with differing keys.
    /// </summary>
    [Fact]
    public void DataTable_FromRecords_UnionsKeysInOrdinalOrderAndFillsMissingWithNull()
    {
        // Arrange
        var records = new List<IReadOnlyDictionary<string, DataValue>>
        {
            new Dictionary<string, DataValue> { ["z"] = DataValue.FromNumber(1) },
            new Dictionary<string, DataValue> { ["m"] = DataValue.FromString("q"), ["z"] = DataValue.FromNumber(2) },
        };

        // Act
        var table = DataTable.FromRecords(records);

        // Assert
        Assert.Equal(["m", "z"], table.Fields);
        Assert.True(table.GetValue(0, 0).IsNull);
        Assert.Equal(1, table.GetNumber(0, 1));
        Assert.Equal("q", table.GetString(1, 0));
    }

    /// <summary>
    ///     Validates that rows without fields are still rows and that the empty table has none.
    /// </summary>
    [Fact]
    public void DataTable_FromRecords_RowsWithoutFieldsAreKept()
    {
        // Arrange / Act
        var table = DataTable.FromRecords([new Dictionary<string, DataValue>(), new Dictionary<string, DataValue>()]);

        // Assert
        Assert.Equal(2, table.RowCount);
        Assert.Equal(0, table.FieldCount);
        Assert.Equal(0, DataTable.Empty.RowCount);
        Assert.Empty(DataTable.Empty.Fields);
    }

    /// <summary>
    ///     Validates that the row and field limits are enforced with the location of the data.
    /// </summary>
    [Fact]
    public void DataTable_Create_LimitsFailClosedWithLocation()
    {
        // Arrange
        var location = new VegaLiteLocation("d.vl.json", "/data/values", 4, 9);
        var rows = Enumerable.Range(0, 3).Select(_ => (IReadOnlyList<DataValue>)[DataValue.FromNumber(1)]);

        // Act / Assert: the third row is over the limit of two
        var rowException = Assert.Throws<VegaLiteRenderException>(() =>
            DataTable.Create(["a"], rows, new VegaLiteLimits { MaxDataRows = 2 }, location));
        Assert.Equal("/data/values", rowException.Location.Pointer);
        Assert.Contains("MaxDataRows", rowException.Message, StringComparison.Ordinal);
        Assert.StartsWith("d.vl.json:4:9", rowException.Message, StringComparison.Ordinal);

        // Assert: three fields are over the limit of two
        var fieldException = Assert.Throws<VegaLiteRenderException>(() =>
            DataTable.Create(["a", "b", "c"], [], new VegaLiteLimits { MaxFieldCount = 2 }, location));
        Assert.Contains("MaxFieldCount", fieldException.Message, StringComparison.Ordinal);

        // Assert: records with too many distinct keys
        Assert.Throws<VegaLiteRenderException>(() =>
            DataTable.FromRecords(
                [new Dictionary<string, DataValue> { ["a"] = default, ["b"] = default, ["c"] = default }],
                new VegaLiteLimits { MaxFieldCount = 2 }));
        Assert.Throws<VegaLiteRenderException>(() =>
            DataTable.FromRecords([new Dictionary<string, DataValue>(), new Dictionary<string, DataValue>()], new VegaLiteLimits { MaxDataRows = 1 }));
    }

    /// <summary>
    ///     Validates that duplicate fields, ragged rows and out-of-range positions are rejected.
    /// </summary>
    [Fact]
    public void DataTable_Create_InvalidArgumentsAreRejected()
    {
        // Arrange
        var table = DataTable.Create(["a"], [[DataValue.FromNumber(1)]]);

        // Act / Assert
        Assert.Throws<ArgumentException>(() => DataTable.Create(["a", "a"], []));
        Assert.Throws<ArgumentException>(() => DataTable.Create(["a", "b"], [[DataValue.FromNumber(1)]]));
        Assert.Throws<ArgumentNullException>(() => DataTable.Create(null!, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => table.GetValue(1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => table.GetValue(0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => table.GetValue(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => table.Column(2));
    }

    /// <summary>
    ///     Validates field lookup by Vega-Lite field path in a table of flattened nested rows.
    /// </summary>
    [Fact]
    public void DataTable_FindField_SupportsDottedBracketedAndEscapedPaths()
    {
        // Arrange: the canonical names a loader gives to the leaves of {"a":{"b":1,"c":[5,6]},"d.e":2}
        var table = DataTable.Create(
            ["a.b", "a.c.0", "a.c.1", "d\\.e"],
            [[DataValue.FromNumber(1), DataValue.FromNumber(5), DataValue.FromNumber(6), DataValue.FromNumber(2)]]);

        // Act / Assert: dotted, bracketed and quoted paths address the nested leaves
        Assert.Equal(table.IndexOfField("a.b"), table.FindField("a.b"));
        Assert.Equal(table.IndexOfField("a.c.1"), table.FindField("a.c[1]"));
        Assert.Equal(table.IndexOfField("a.c.0"), table.FindField("a['c'][0]"));
        Assert.Equal(table.IndexOfField("a.b"), table.FindField("a[\"b\"]"));
        Assert.Equal(6, table.GetValue(0, "a.c[1]").Number);

        // Assert: an escaped dot is part of the name, an unescaped one is a path separator
        Assert.Equal(table.IndexOfField("d\\.e"), table.FindField("d\\.e"));
        Assert.Equal(-1, table.FindField("d.e"));

        // Assert: malformed and empty paths address nothing
        Assert.Equal(-1, table.FindField("a[0"));
        Assert.Equal(-1, table.FindField(string.Empty));
        Assert.Equal(-1, table.FindField(null));
        Assert.True(table.GetValue(0, "nope").IsNull);
    }

    /// <summary>
    ///     Validates how field paths are split.
    /// </summary>
    [Fact]
    public void FieldPath_Parse_SplitsPathsLikeVega()
    {
        // Arrange / Act / Assert
        Assert.Equal(["a", "b", "c"], FieldPath.Parse("a.b.c"));
        Assert.Equal(["a", "0", "b"], FieldPath.Parse("a[0].b"));
        Assert.Equal(["a.b"], FieldPath.Parse("a\\.b"));
        Assert.Equal(["a", "b.c"], FieldPath.Parse("a['b.c']"));
        Assert.Equal(["a]"], FieldPath.Parse("a\\]"));
        Assert.Equal(["x", string.Empty], FieldPath.Parse("x[\"\"]"));
        Assert.Equal(["a", "b"], FieldPath.Parse(".a..b"));
        Assert.Equal(["a\\b"], FieldPath.Parse("a\\\\b"));
        Assert.Empty(FieldPath.Parse(string.Empty)!);
        Assert.Null(FieldPath.Parse("a[0"));
        Assert.Null(FieldPath.Parse("a]"));
        Assert.Null(FieldPath.Parse("a['b"));
        Assert.Null(FieldPath.Parse(null));

        // Assert: the canonical text escapes backslashes and dots so that different paths never collide
        Assert.Equal("a.b", FieldPath.Canonicalize("a[\"b\"]"));
        Assert.Equal("a\\.b", FieldPath.Canonicalize("a\\.b"));
        Assert.Equal("a\\\\b", FieldPath.Canonicalize("a\\\\b"));
        Assert.Null(FieldPath.Canonicalize(string.Empty));
        Assert.Equal("p.q.0", FieldPath.Join(["p", "q", "0"]));
        Assert.Equal("x\\.y.z", FieldPath.Join(["x.y", "z"]));
    }

    /// <summary>
    ///     Validates that numbers are formatted as JavaScript's <c>String(number)</c> does, whatever the current culture.
    /// </summary>
    [Fact]
    public void JsNumber_ToString_MatchesJavaScriptNumberFormatting()
    {
        // Arrange: pairs written from the JavaScript Number-to-String rules (shortest digits; exponent form below 1e-6 and from 1e21)
        var vectors = new (double Value, string Expected)[]
        {
            (1, "1"),
            (0.5, "0.5"),
            (-2, "-2"),
            (0, "0"),
            (-0.0, "0"),
            (100, "100"),
            (123456789, "123456789"),
            (0.000001, "0.000001"),
            (0.0000001, "1e-7"),
            (1.5e-7, "1.5e-7"),
            (123456789012345680000.0, "123456789012345680000"),
            (1e21, "1e+21"),
            (1.2345e25, "1.2345e+25"),
            (-1e21, "-1e+21"),
            (0.1 + 0.2, "0.30000000000000004"),
            (1.0 / 3, "0.3333333333333333"),
            (5e-324, "5e-324"),
            (double.NaN, "NaN"),
            (double.PositiveInfinity, "Infinity"),
            (double.NegativeInfinity, "-Infinity"),
        };
        var original = CultureInfo.CurrentCulture;
        try
        {
            // Act / Assert: a culture with a decimal comma and a different minus sign must not matter
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            foreach (var (value, expected) in vectors)
            {
                Assert.Equal(expected, JsNumber.ToString(value));
                Assert.Equal(expected, DataValue.FromNumber(value).ToText());
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    /// <summary>
    ///     Validates the text, ordering, equality and numeric reading of scalar values.
    /// </summary>
    [Fact]
    public void DataValue_TextOrderingEqualityAndQuantitativeReading_FollowTheDocumentedRules()
    {
        // Arrange / Act / Assert: text
        Assert.Equal("null", DataValue.Null.ToText());
        Assert.Equal("true", DataValue.FromBoolean(true).ToText());
        Assert.Equal("false", DataValue.FromBoolean(false).ToString());
        Assert.Equal("abc", DataValue.FromString("abc").ToText());
        Assert.True(DataValue.FromString(null).IsNull);

        // Assert: ordering is null, boolean, number, string between kinds and natural within a kind (ordinal strings, NaN first)
        var shuffled = new[]
        {
            DataValue.FromString("b"),
            DataValue.FromNumber(10),
            DataValue.FromString("B"),
            DataValue.FromNumber(9),
            DataValue.FromBoolean(true),
            DataValue.Null,
            DataValue.FromNumber(double.NaN),
            DataValue.FromBoolean(false),
        };
        var sorted = shuffled.Order(Comparer<DataValue>.Create(DataValue.Compare)).Select(v => v.ToText()).ToArray();
        Assert.Equal(["null", "false", "true", "NaN", "9", "10", "B", "b"], sorted);

        // Assert: equality is the same kind and value, with zero equal to negative zero and NaN equal to NaN
        Assert.Equal(DataValue.FromNumber(0), DataValue.FromNumber(-0.0));
        Assert.Equal(DataValue.FromNumber(0).GetHashCode(), DataValue.FromNumber(-0.0).GetHashCode());
        Assert.Equal(DataValue.FromNumber(double.NaN), DataValue.FromNumber(double.NaN));
        Assert.NotEqual(DataValue.FromNumber(1), DataValue.FromString("1"));
        Assert.NotEqual(DataValue.FromBoolean(true), DataValue.FromNumber(1));
        Assert.True(DataValue.FromString("a") == DataValue.FromString("a"));
        Assert.True(DataValue.Null != DataValue.FromBoolean(false));

        // Assert: quantitative reading takes numbers, booleans and numeric strings, and nothing else
        Assert.True(DataValue.FromNumber(2.5).TryGetQuantitative(out var number));
        Assert.Equal(2.5, number);
        Assert.True(DataValue.FromString(" 12.5 ").TryGetQuantitative(out number));
        Assert.Equal(12.5, number);
        Assert.True(DataValue.FromString("1e3").TryGetQuantitative(out number));
        Assert.Equal(1000, number);
        Assert.True(DataValue.FromBoolean(true).TryGetQuantitative(out number));
        Assert.Equal(1, number);
        Assert.False(DataValue.FromNumber(double.NaN).TryGetQuantitative(out _));
        Assert.False(DataValue.FromNumber(double.PositiveInfinity).TryGetQuantitative(out _));
        Assert.False(DataValue.FromString("abc").TryGetQuantitative(out _));
        Assert.False(DataValue.FromString("  ").TryGetQuantitative(out _));
        Assert.False(DataValue.Null.TryGetQuantitative(out _));
    }

    /// <summary>
    ///     Validates JavaScript's rounding and radix helpers.
    /// </summary>
    [Fact]
    public void JsNumber_RoundAndRadix_FollowJavaScript()
    {
        // Arrange / Act / Assert: Math.round rounds half-way cases toward positive infinity
        Assert.Equal(3, JsNumber.Round(2.5));
        Assert.Equal(-2, JsNumber.Round(-2.5));
        Assert.Equal(0, JsNumber.Round(0.49999999999999994));
        Assert.Equal(1, JsNumber.Round(0.5));
        Assert.True(double.IsNegative(JsNumber.Round(-0.3)));
        Assert.True(double.IsNaN(JsNumber.Round(double.NaN)));

        // Assert: radix conversion of exact integers, including above 2^53
        Assert.Equal("0", JsNumber.ToRadix(0, 2));
        Assert.Equal("1010", JsNumber.ToRadix(10, 2));
        Assert.Equal("377", JsNumber.ToRadix(255, 8));
        Assert.Equal("ff", JsNumber.ToRadix(255, 16));
        Assert.Equal("20000000000000", JsNumber.ToRadix(9007199254740992, 16));
    }
}
