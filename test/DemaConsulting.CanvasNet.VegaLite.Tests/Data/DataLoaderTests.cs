using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Data;

/// <summary>
///     Tests of the data loader: the rows it reads from inline values, local CSV, TSV, DSV and JSON files, named datasets
///     and sequences, the sources it only reports, and its limits. Where the feature catalog is responsible for a report,
///     the test also checks the report through <see cref="VegaLiteDocument"/>.
/// </summary>
public class DataLoaderTests
{
    /// <summary>Gets the cancellation token of the running test.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Validates that inline values keep their JSON types, that a missing field is null, and that fields are in
    ///     ordinal order.
    /// </summary>
    [Fact]
    public void DataLoader_Load_InlineValuesPreservesJsonTypes()
    {
        // Arrange: rows with every JSON scalar type, one row with fewer fields, numbers in several spellings
        const string json = """
            {
              "data": { "values": [
                { "n": 1, "s": "x", "b": true, "z": null, "f": 2.5 },
                { "n": -3e2, "s": "", "b": false, "f": 0.1 },
                { "s": "late" }
              ] },
              "mark": "point"
            }
            """;
        var tree = SpecParser.Parse(json);

        // Act
        var table = DataLoader.Load(tree.Root["data"], tree.SourceName, null, Ct);

        // Assert: three rows, fields in ordinal order
        Assert.Equal(3, table.RowCount);
        Assert.Equal(["b", "f", "n", "s", "z"], table.Fields);

        // Assert: each value has the JSON type it was written with
        Assert.Equal(1, table.GetValue(0, "n").Number);
        Assert.Equal(DataValueKind.Number, table.GetValue(0, "n").Kind);
        Assert.Equal("x", table.GetValue(0, "s").Text);
        Assert.True(table.GetValue(0, "b").Boolean);
        Assert.Equal(DataValueKind.Null, table.GetValue(0, "z").Kind);
        Assert.Equal(2.5, table.GetValue(0, "f").Number);
        Assert.Equal(-300, table.GetValue(1, "n").Number);
        Assert.Equal(DataValueKind.String, table.GetValue(1, "s").Kind);
        Assert.Equal(string.Empty, table.GetValue(1, "s").Text);
        Assert.Equal(DataValueKind.Boolean, table.GetValue(1, "b").Kind);
        Assert.False(table.GetValue(1, "b").Boolean);

        // Assert: a field the row does not have is null, whatever its type elsewhere
        Assert.True(table.GetValue(1, "z").IsNull);
        Assert.True(table.GetValue(2, "n").IsNull);
        Assert.Equal("late", table.GetValue(2, "s").Text);
    }

    /// <summary>
    ///     Validates that primitives become rows with the field <c>data</c>, as Vega-Lite does.
    /// </summary>
    [Fact]
    public void DataLoader_Load_PrimitiveValuesBecomeRowsWithTheDataField()
    {
        // Arrange
        var tree = SpecParser.Parse("{\"data\":{\"values\":[3, \"a\", true, null, 1.5]}}");

        // Act
        var table = DataLoader.Load(tree.Root["data"], null, null, Ct);

        // Assert
        Assert.Equal(["data"], table.Fields);
        Assert.Equal(5, table.RowCount);
        Assert.Equal(["3", "a", "true", "null", "1.5"], table.Column(0).Select(v => v.ToText()).ToArray());
        Assert.Equal(DataValueKind.Number, table.GetValue(0, "data").Kind);
        Assert.Equal(DataValueKind.String, table.GetValue(1, "data").Kind);
        Assert.Equal(DataLoader.PrimitiveField, table.Fields[0]);
    }

    /// <summary>
    ///     Validates that nested objects and arrays are flattened into fields named by their canonical path, and that a
    ///     key containing a dot stays distinct from a nested path.
    /// </summary>
    [Fact]
    public void DataLoader_Load_NestedObjectsAreFlattenedToPathFields()
    {
        // Arrange
        const string json = """{"data":{"values":[{"p":{"q":1,"r":[10,20]},"s.t":3,"u":{"v":{"w":"deep"}},"e":{},"a":[]}]}}""";
        var tree = SpecParser.Parse(json);

        // Act
        var table = DataLoader.Load(tree.Root["data"], null, null, Ct);

        // Assert: one field per leaf; empty containers add none
        Assert.Equal(["p.q", "p.r.0", "p.r.1", "s\\.t", "u.v.w"], table.Fields);
        Assert.Equal(1, table.GetValue(0, "p.q").Number);
        Assert.Equal(20, table.GetValue(0, "p.r[1]").Number);
        Assert.Equal("deep", table.GetValue(0, "u.v.w").Text);

        // Assert: a literal dot in a key needs the escape; without it the path is nested and finds nothing
        Assert.Equal(3, table.GetValue(0, "s\\.t").Number);
        Assert.True(table.GetValue(0, "s.t").IsNull);
    }

    /// <summary>
    ///     Validates that missing, empty and non-object data give an empty table.
    /// </summary>
    [Fact]
    public void DataLoader_Load_MissingDataGivesAnEmptyTable()
    {
        // Arrange
        var tree = SpecParser.Parse("{\"data\":null,\"mark\":\"point\"}");

        // Act / Assert
        Assert.Same(DataTable.Empty, DataLoader.Load(null, null, null, Ct));
        Assert.Same(DataTable.Empty, DataLoader.Load(tree.Root["data"], null, null, Ct));
        Assert.Same(DataTable.Empty, DataLoader.Load(tree.Root["mark"], null, null, Ct));
    }

    /// <summary>
    ///     Validates that an empty values array loads as an empty table and that the document reports it as
    ///     <c>vl-data-empty</c> while still rendering.
    /// </summary>
    [Fact]
    public void DataLoader_Load_EmptyValuesReportsVlDataEmpty()
    {
        // Arrange
        const string json = """{"data":{"values":[]},"mark":"point","encoding":{"x":{"field":"a","type":"quantitative"}}}""";
        var tree = SpecParser.Parse(json);

        // Act
        var table = DataLoader.Load(tree.Root["data"], null, null, Ct);
        var warnings = new List<VegaLiteDiagnostic>();
        using var surface = VegaLiteDocument.Parse(json).Render(new VegaLiteRenderOptions { WarningHandler = warnings.Add }, Ct);

        // Assert: the loader yields an empty table and the document reports it as a warning with the pointer of the data
        Assert.Equal(0, table.RowCount);
        Assert.Equal(0, table.FieldCount);
        var warning = Assert.Single(warnings);
        Assert.StartsWith("vl-data-empty:", warning.Message, StringComparison.Ordinal);
        Assert.Equal("/data", warning.Location.Pointer);
        Assert.True(surface.Width > 0);
    }

    /// <summary>
    ///     Validates that a remote URL is never read: the table is empty and the document reports
    ///     <c>vl-data-remote-url</c>, which strict mode turns into an exception.
    /// </summary>
    [Fact]
    public void DataLoader_Load_RemoteUrlReportsVlDataRemoteUrl()
    {
        // Arrange
        const string json = """{"data":{"url":"https://example.invalid/rows.json"},"mark":"point"}""";
        var tree = SpecParser.Parse(json);

        // Act
        var table = DataLoader.Load(tree.Root["data"], null, null, Ct);
        var reports = new List<VegaLiteUnsupportedFeature>();
        using var surface = VegaLiteDocument.Parse(json).Render(new VegaLiteRenderOptions { UnsupportedFeatureHandler = reports.Add }, Ct);

        // Assert: nothing was loaded and the report names the feature and the data location
        Assert.Equal(0, table.RowCount);
        var report = Assert.Single(reports);
        Assert.Equal("vl-data-remote-url", report.Feature);
        Assert.Equal("/data", report.Location.Pointer);
        var ex = Assert.Throws<VegaLiteUnsupportedFeatureException>(() =>
            VegaLiteDocument.Parse(json).Render(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true }, Ct));
        Assert.Equal("vl-data-remote-url", ex.Feature);
    }

    /// <summary>
    ///     Validates that the sources that are only reported (text and object values, geographic generators, remote data) are
    ///     empty tables and are reported by the catalog.
    /// </summary>
    [Theory]
    [InlineData("""{"values":"a,b\n1,2"}""", "vl-data-values-text")]
    [InlineData("""{"values":{"type":"Topology"}}""", "vl-data-geo")]
    [InlineData("""{"sphere":true}""", "vl-data-geo")]
    [InlineData("""{"url":"rows.topojson","format":{"type":"topojson"}}""", "vl-data-geo")]
    [InlineData("""{"url":"\\\\server\\share\\rows.json"}""", "vl-data-remote-url")]
    [InlineData("""{"url":"https://example.org/rows.csv"}""", "vl-data-remote-url")]
    [InlineData("""{"url":"/etc/hosts"}""", "vl-data-remote-url")]
    public void DataLoader_Load_OtherSourcesAreEmptyTablesReportedByTheCatalog(string dataJson, string expectedFeature)
    {
        // Arrange: a document with a resolver callback that must never be asked (nothing here may touch a file)
        var json = "{\"data\":" + dataJson + ",\"mark\":\"point\"}";
        var tree = SpecParser.Parse(json);
        var asked = new List<string>();
        var open = new VegaLiteOpenOptions { FileResolver = path => { asked.Add(path); return null; } };

        // Act
        var table = new DataLoader(null, null, open, cancellationToken: Ct).Load(tree.Root["data"]);
        var features = FeatureCatalog.Visit(tree).Select(r => r.Id).ToList();

        // Assert
        Assert.Same(DataTable.Empty, table);
        Assert.Contains(expectedFeature, features);
        Assert.Empty(asked);
    }

    /// <summary>
    ///     Validates that the row limit is enforced before any row is converted and carries the location of the values.
    /// </summary>
    [Fact]
    public void DataLoader_Load_RowLimitFailsWithLocation()
    {
        // Arrange
        const string json = "{\n  \"data\": { \"values\": [ {\"a\":1}, {\"a\":2}, {\"a\":3} ] }\n}";
        var tree = SpecParser.Parse(json, "rows.vl.json");

        // Act / Assert
        var ex = Assert.Throws<VegaLiteRenderException>(() =>
            DataLoader.Load(tree.Root["data"], tree.SourceName, new VegaLiteLimits { MaxDataRows = 2 }, Ct));
        Assert.Equal("/data/values", ex.Location.Pointer);
        Assert.Equal("rows.vl.json", ex.Location.File);
        Assert.Equal(2, ex.Location.Line);
        Assert.Contains("MaxDataRows", ex.Message, StringComparison.Ordinal);
        Assert.StartsWith("rows.vl.json:2:", ex.Message, StringComparison.Ordinal);

        // Assert: exactly the limit is accepted
        Assert.Equal(3, DataLoader.Load(tree.Root["data"], null, new VegaLiteLimits { MaxDataRows = 3 }, Ct).RowCount);
    }

    /// <summary>
    ///     Validates that a row with too many fields fails with the location of the leaf that exceeded the limit.
    /// </summary>
    [Fact]
    public void DataLoader_Load_FieldLimitFailsWithLocationOfTheLeaf()
    {
        // Arrange
        const string json = """{"data":{"values":[{"a":1,"b":2,"c":3}]}}""";
        var tree = SpecParser.Parse(json);

        // Act / Assert
        var ex = Assert.Throws<VegaLiteRenderException>(() =>
            DataLoader.Load(tree.Root["data"], null, new VegaLiteLimits { MaxFieldCount = 2 }, Ct));
        Assert.Equal("/data/values/0/c", ex.Location.Pointer);
        Assert.Contains("MaxFieldCount", ex.Message, StringComparison.Ordinal);

        // Assert: the limit also applies to the union of the fields of different rows
        var tree2 = SpecParser.Parse("""{"data":{"values":[{"a":1},{"b":2},{"c":3}]}}""");
        Assert.Throws<VegaLiteRenderException>(() =>
            DataLoader.Load(tree2.Root["data"], null, new VegaLiteLimits { MaxFieldCount = 2 }, Ct));
    }

    /// <summary>
    ///     Validates that loading honors cancellation.
    /// </summary>
    [Fact]
    public void DataLoader_Load_CanceledTokenStopsLoading()
    {
        // Arrange: enough rows to reach a cancellation check
        var rows = string.Join(",", Enumerable.Range(0, 3000).Select(i => "{\"a\":" + i + "}"));
        var tree = SpecParser.Parse("{\"data\":{\"values\":[" + rows + "]}}");
        using var source = new CancellationTokenSource();
        source.Cancel();

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => DataLoader.Load(tree.Root["data"], null, null, source.Token));
    }

    /// <summary>
    ///     Validates that a large inline table loads completely and quickly.
    /// </summary>
    [Fact]
    public void DataLoader_Load_LargeInlineTableLoadsAllRows()
    {
        // Arrange
        var rows = string.Join(",", Enumerable.Range(0, 20_000).Select(i => "{\"k\":\"c" + i + "\",\"v\":" + i + "}"));
        var tree = SpecParser.Parse("{\"data\":{\"values\":[" + rows + "]}}");

        // Act
        var table = DataLoader.Load(tree.Root["data"], null, null, Ct);

        // Assert
        Assert.Equal(20_000, table.RowCount);
        Assert.Equal("c19999", table.GetValue(19_999, "k").Text);
        Assert.Equal(19_999, table.GetValue(19_999, "v").Number);
    }

    /// <summary>The location the tests build for virtual files.</summary>
    private const string SpecName = "spec.vl.json";

    /// <summary>
    ///     Creates a loader over a specification tree with open options and optional datasets.
    /// </summary>
    /// <param name="tree">The specification.</param>
    /// <param name="open">The open options, or null for none.</param>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <param name="caller">The caller's datasets, or null.</param>
    /// <returns>The loader.</returns>
    private static DataLoader CreateLoader(
        SpecTree tree,
        VegaLiteOpenOptions? open = null,
        VegaLiteLimits? limits = null,
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>>? caller = null) =>
        new(tree.SourceName, limits, open, tree.Root["datasets"], caller, Ct);

    /// <summary>
    ///     Parses a view with the given <c>data</c> object text.
    /// </summary>
    /// <param name="dataJson">The text of the <c>data</c> object.</param>
    /// <param name="extra">Extra top-level members (such as <c>datasets</c>), starting with a comma, or empty.</param>
    /// <returns>The tree.</returns>
    private static SpecTree Spec(string dataJson, string extra = "") =>
        SpecParser.Parse("{\"data\":" + dataJson + ",\"mark\":\"point\"" + extra + "}", SpecName);

    /// <summary>
    ///     Creates open options over a virtual file set.
    /// </summary>
    /// <param name="files">The files.</param>
    /// <returns>The options.</returns>
    private static VegaLiteOpenOptions Virtual(VirtualDataFiles files) => new() { FileResolver = files.Resolve };

    /// <summary>
    ///     Loads the <c>data</c> object of a view with a fresh loader.
    /// </summary>
    /// <param name="dataJson">The text of the <c>data</c> object.</param>
    /// <param name="open">The open options, or null for none.</param>
    /// <param name="limits">The limits, or null for the defaults.</param>
    /// <returns>The table.</returns>
    private static DataTable LoadData(string dataJson, VegaLiteOpenOptions? open = null, VegaLiteLimits? limits = null)
    {
        var tree = Spec(dataJson);
        return CreateLoader(tree, open, limits).Load(tree.Root["data"]);
    }

    /// <summary>
    ///     Validates that a relative <c>url</c> is read from the base directory, with the format taken from
    ///     <c>format.type</c> or else from the extension, and that the cells are typed by per-column inference when
    ///     <c>parse</c> is <c>"auto"</c>.
    /// </summary>
    [Fact]
    public void DataLoader_Load_LocalCsvUrlUsesRelativeFile()
    {
        // Arrange: a CSV with a quoted comma, a TSV, a semicolon DSV and a CSV whose extension is misleading
        using var directory = new DataTempDirectory();
        directory.Write("sub/t.csv", "k,v,ok,day\r\n\"a,x\",1,true,2024-01-01\r\nb,2.5,false,2024-01-02\r\n");
        directory.Write("t.tsv", "k\tv\na\t7\n");
        directory.Write("t.txt", "k;v\na;8\n");
        directory.Write("really.json", "k,v\na,9\n");
        var open = new VegaLiteOpenOptions { BaseDirectory = directory.Path };

        // Act: inference is an explicit opt-in
        var csv = LoadData("{\"url\":\"sub/t.csv\",\"format\":{\"parse\":\"auto\"}}", open);
        var tsv = LoadData("{\"url\":\"t.tsv\",\"format\":{\"parse\":\"auto\"}}", open);
        var dsv = LoadData("{\"url\":\"t.txt\",\"format\":{\"type\":\"dsv\",\"delimiter\":\";\",\"parse\":\"auto\"}}", open);
        var typed = LoadData("{\"url\":\"really.json\",\"format\":{\"type\":\"csv\",\"parse\":\"auto\"}}", open);

        // Assert: the CSV has typed columns
        Assert.Equal(2, csv.RowCount);
        Assert.Equal(["day", "k", "ok", "v"], csv.Fields);
        Assert.Equal("a,x", csv.GetValue(0, "k").Text);
        Assert.Equal(2.5, csv.GetValue(1, "v").Number);
        Assert.True(csv.GetValue(0, "ok").Boolean);
        Assert.Equal(DataValueKind.Date, csv.GetValue(1, "day").Kind);
        Assert.Equal("2024-01-02T00:00:00.000Z", csv.GetValue(1, "day").ToText());

        // Assert: the extension chooses TSV, a DSV names its delimiter, and format.type beats the extension
        Assert.Equal(7, tsv.GetValue(0, "v").Number);
        Assert.Equal(8, dsv.GetValue(0, "v").Number);
        Assert.Equal(9, typed.GetValue(0, "v").Number);
    }

    /// <summary>
    ///     Validates that delimited text without <c>parse</c> (or with <c>parse: null</c>) keeps every cell as text, as
    ///     Vega does, and that a field map converts only the named field.
    /// </summary>
    [Fact]
    public void DataLoader_Load_DefaultDelimitedTextKeepsTextValues()
    {
        // Arrange: text that inference would turn into numbers, a date and a boolean
        var files = new VirtualDataFiles().With("t.csv", "zip,short,day,flag,n\n01234,007,2024-03-05,true,7\n98765,008,2024-03-06T10:00:00Z,false,8\n");
        var open = Virtual(files);

        // Act
        var absent = LoadData("{\"url\":\"t.csv\"}", open);
        var nullParse = LoadData("{\"url\":\"t.csv\",\"format\":{\"parse\":null}}", open);
        var named = LoadData("{\"url\":\"t.csv\",\"format\":{\"parse\":{\"n\":\"number\"}}}", open);
        var dsv = LoadData("{\"url\":\"t.csv\",\"format\":{\"type\":\"dsv\",\"delimiter\":\",\"}}", open);

        // Assert: nothing is converted by default, whatever way the format is written
        foreach (var table in new[] { absent, nullParse, dsv })
        {
            Assert.Equal(2, table.RowCount);
            foreach (var field in table.Fields)
            {
                Assert.Equal(DataValueKind.String, table.GetValue(0, field).Kind);
            }

            Assert.Equal("01234", table.GetValue(0, "zip").Text);
            Assert.Equal("007", table.GetValue(0, "short").Text);
            Assert.Equal("2024-03-05", table.GetValue(0, "day").Text);
            Assert.Equal("2024-03-06T10:00:00Z", table.GetValue(1, "day").Text);
            Assert.Equal("true", table.GetValue(0, "flag").Text);
            Assert.Equal("7", table.GetValue(0, "n").Text);
        }

        // Assert: an explicit map affects the named field only
        Assert.Equal(7, named.GetValue(0, "n").Number);
        Assert.Equal("01234", named.GetValue(0, "zip").Text);
        Assert.Equal("true", named.GetValue(0, "flag").Text);
    }

    /// <summary>
    ///     Validates the format guessed from the extension of a path.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="expected">The format type.</param>
    [Theory]
    [InlineData("a.csv", "csv")]
    [InlineData("dir/A.CSV", "csv")]
    [InlineData("a.tsv", "tsv")]
    [InlineData("a.TSV", "tsv")]
    [InlineData("a.json", "json")]
    [InlineData("a.txt", "json")]
    [InlineData("a", "json")]
    [InlineData("data.csv/rows", "json")]
    public void DataLoader_InferFormatType_UsesTheExtension(string path, string expected)
    {
        // Arrange / Act
        var type = DataLoader.InferFormatType(path);

        // Assert
        Assert.Equal(expected, type);
    }

    /// <summary>
    ///     Validates the format properties of JSON data files: a dotted property path selects the rows (also through an
    ///     array index and from the fixture file), explicit parse maps convert strings, and JSON types are otherwise kept.
    /// </summary>
    [Fact]
    public void DataLoader_Load_JsonPropertyAndExplicitParseAreApplied()
    {
        // Arrange: a nested document with strings that the parse map turns into a number, a date and a boolean
        var files = new VirtualDataFiles()
            .With("nested.json", """{"a":[{"rows":[{"n":"5","d":"2024-01-02","b":"true","keep":"7","k":1}]}]}""")
            .With("plain.json", """[{"x":1},{"x":"2"}]""");
        var fixtures = new VegaLiteOpenOptions { BaseDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures") };

        // Act
        var parsed = LoadData(
            """{"url":"nested.json","format":{"property":"a[0].rows","parse":{"n":"number","d":"date","b":"boolean"}}}""", Virtual(files));
        var plain = LoadData("""{"url":"plain.json"}""", Virtual(files));
        var fixture = LoadData("""{"url":"data-values.json","format":{"property":"report.rows"}}""", fixtures);

        // Assert: the mapped fields have the requested types; the others keep the JSON type
        Assert.Equal(5, parsed.GetValue(0, "n").Number);
        Assert.Equal(DataValueKind.Date, parsed.GetValue(0, "d").Kind);
        Assert.True(parsed.GetValue(0, "b").Boolean);
        Assert.Equal("7", parsed.GetValue(0, "keep").Text);
        Assert.Equal(1, parsed.GetValue(0, "k").Number);

        // Assert: JSON is not inferred by default (a string stays a string); a number stays a number
        Assert.Equal(DataValueKind.Number, plain.GetValue(0, "x").Kind);
        Assert.Equal("2", plain.GetValue(1, "x").Text);

        // Assert: the fixture's rows are found under report.rows
        Assert.Equal(4, fixture.RowCount);
        Assert.Equal(91, fixture.GetValue(3, "crates").Number);
    }

    /// <summary>
    ///     Validates that data files that are not usable are located failures: malformed JSON, a document that is not an
    ///     array, a missing property, a number outside the double range and too deep nesting.
    /// </summary>
    /// <param name="content">The content of the file.</param>
    /// <param name="format">The text of the format object.</param>
    /// <param name="expected">Part of the expected message.</param>
    [Theory]
    [InlineData("""[{"a":1},""", "{}", "is not valid JSON")]
    [InlineData("""{"rows":[]}""", "{}", "must be an array of rows")]
    [InlineData("""{"rows":[]}""", """{"property":"nope"}""", "has no property 'nope'")]
    [InlineData("""{"rows":[]}""", """{"property":"rows[3]"}""", "has no property 'rows[3]'")]
    [InlineData("""{"rows":{}}""", """{"property":"rows"}""", "must be an array of rows")]
    [InlineData("""{"rows":[]}""", """{"property":"rows[unclosed"}""", "is not a valid field path")]
    [InlineData("""[{"a":1e999}]""", "{}", "outside the range of a double")]
    [InlineData("""[[[[[[[[[[1]]]]]]]]]]""", "{}", "nested deeper")]
    public void DataLoader_Load_UnusableJsonFileIsALocatedFailure(string content, string format, string expected)
    {
        // Arrange
        var files = new VirtualDataFiles().With("d.json", content);
        var tree = Spec("{\"url\":\"d.json\",\"format\":" + format + "}");
        var loader = new DataLoader(tree.SourceName, new VegaLiteLimits { MaxJsonDepth = 5 }, Virtual(files), cancellationToken: Ct);

        // Act
        var ex = Assert.Throws<VegaLiteRenderException>(() => loader.Load(tree.Root["data"]));

        // Assert: the failure points at the url value in the specification
        Assert.Contains(expected, ex.Message, StringComparison.Ordinal);
        Assert.Equal("/data/url", ex.Location.Pointer);
        Assert.Equal(SpecName, ex.Location.File);
    }

    /// <summary>
    ///     Validates that named datasets come from the caller first and else from the top-level <c>datasets</c> of the
    ///     specification, that the caller's values keep their kinds (dates in UTC), and that a dataset of the
    ///     specification that is not an array is empty (the catalog reports it).
    /// </summary>
    [Fact]
    public void DataLoader_Load_NamedDatasetsUseCallerPrecedence()
    {
        // Arrange: "both" is defined by the specification and by the caller, "spec" and "text" only by the specification
        var tree = Spec(
            "{\"name\":\"both\"}",
            ",\"datasets\":{\"both\":[{\"v\":1}],\"spec\":[{\"s\":\"x\"},{\"s\":\"y\"}],\"text\":\"a,b\"}");
        var caller = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>>
        {
            ["both"] = new List<IReadOnlyDictionary<string, object?>>
            {
                new Dictionary<string, object?>
                {
                    ["int"] = 3,
                    ["long"] = 4L,
                    ["decimal"] = 2.5m,
                    ["text"] = "t",
                    ["flag"] = true,
                    ["nothing"] = null,
                    ["unspecified"] = new DateTime(2024, 3, 5, 0, 0, 0, DateTimeKind.Unspecified),
                    ["utc"] = new DateTime(2024, 3, 5, 12, 0, 0, DateTimeKind.Utc),
                    ["offset"] = new DateTimeOffset(2024, 3, 5, 12, 0, 0, TimeSpan.FromHours(2)),
                    ["a.b"] = 1,
                },
            },
        };

        // Act
        var both = CreateLoader(tree, caller: caller).Load(tree.Root["data"]);
        var specOnly = CreateLoader(tree).Load(Spec("{\"name\":\"spec\"}").Root["data"]);
        var text = CreateLoader(tree).Load(Spec("{\"name\":\"text\"}").Root["data"]);

        // Assert: the caller's dataset replaces the specification's one of the same name (the field v is gone)
        Assert.Equal(1, both.RowCount);
        Assert.DoesNotContain("v", both.Fields);
        Assert.Equal(3, both.GetValue(0, "int").Number);
        Assert.Equal(4, both.GetValue(0, "long").Number);
        Assert.Equal(2.5, both.GetValue(0, "decimal").Number);
        Assert.Equal("t", both.GetValue(0, "text").Text);
        Assert.True(both.GetValue(0, "flag").Boolean);
        Assert.True(both.GetValue(0, "nothing").IsNull);
        Assert.Equal("2024-03-05T00:00:00.000Z", both.GetValue(0, "unspecified").ToText());
        Assert.Equal("2024-03-05T12:00:00.000Z", both.GetValue(0, "utc").ToText());
        Assert.Equal("2024-03-05T10:00:00.000Z", both.GetValue(0, "offset").ToText());
        Assert.Equal(1, both.GetValue(0, "a\\.b").Number);

        // Assert: without a caller dataset the specification's is used; a text dataset is empty
        Assert.Equal(["x", "y"], specOnly.Column(0).Select(v => v.Text));
        Assert.Same(DataTable.Empty, text);
    }

    /// <summary>
    ///     Validates that the largest values of the .NET date types (the usual "no end" sentinels) are clamped to the
    ///     largest supported date and never fail, whatever their kind.
    /// </summary>
    [Fact]
    public void DataLoader_Load_CallerDateTimeMaxValuesClampToSupportedDate()
    {
        // Arrange: both sentinels, the exact maximum millisecond, and a local-kind maximum
        var tree = Spec("{\"name\":\"d\"}");
        var maxMillisecond = new DateTime(9999, 12, 31, 23, 59, 59, 999, DateTimeKind.Utc);
        var caller = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>>
        {
            ["d"] = new List<IReadOnlyDictionary<string, object?>>
            {
                new Dictionary<string, object?>
                {
                    ["dateTime"] = DateTime.MaxValue,
                    ["offset"] = DateTimeOffset.MaxValue,
                    ["exact"] = maxMillisecond,
                    ["local"] = DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Local),
                    ["min"] = DateTime.MinValue,
                },
            },
        };

        // Act
        var table = CreateLoader(tree, caller: caller).Load(tree.Root["data"]);

        // Assert: every maximum is the last supported millisecond (as a date and as text), the minimum is the first
        foreach (var field in new[] { "dateTime", "offset", "exact", "local" })
        {
            Assert.Equal(DataValueKind.Date, table.GetValue(0, field).Kind);
            Assert.Equal(253402300799999.0, table.GetValue(0, field).Milliseconds);
            Assert.Equal("9999-12-31T23:59:59.999Z", table.GetValue(0, field).ToText());
        }

        Assert.Equal("0001-01-01T00:00:00.000Z", table.GetValue(0, "min").ToText());
    }

    /// <summary>
    ///     Validates that a JSON data file with an escaped lone surrogate in a string value is a located failure about
    ///     invalid Unicode, never a raw <see cref="InvalidOperationException"/>.
    /// </summary>
    [Fact]
    public void DataLoader_Load_JsonLoneSurrogateStringIsLocatedUnicodeFailure() =>
        AssertUnicodeFailure("""[{"a":"ok"},{"a":"\ud800"}]""", "{}");

    /// <summary>
    ///     Validates that a JSON data file with an escaped lone surrogate in a property name is a located failure about
    ///     invalid Unicode.
    /// </summary>
    [Fact]
    public void DataLoader_Load_JsonLoneSurrogatePropertyNameIsLocatedUnicodeFailure() =>
        AssertUnicodeFailure("""[{"\udc00":1}]""", "{}");

    /// <summary>
    ///     Validates that a JSON data file whose <c>format.property</c> lookup has to compare a property name that holds an
    ///     escaped lone surrogate is a located failure about invalid Unicode.
    /// </summary>
    [Fact]
    public void DataLoader_Load_JsonLoneSurrogatePropertyLookupIsLocatedUnicodeFailure() =>
        AssertUnicodeFailure("""{"rows":[{"a":1}],"\ud800":1}""", """{"property":"rows"}""");

    /// <summary>
    ///     Loads a JSON file that must fail with the located invalid-Unicode failure and checks the failure.
    /// </summary>
    /// <param name="content">The content of the file.</param>
    /// <param name="format">The text of the format object.</param>
    private static void AssertUnicodeFailure(string content, string format)
    {
        // Arrange
        var files = new VirtualDataFiles().With("d.json", content);
        var tree = Spec("{\"url\":\"d.json\",\"format\":" + format + "}");
        var loader = new DataLoader(tree.SourceName, cancellationToken: Ct, openOptions: Virtual(files));

        // Act: a raw InvalidOperationException would not be a VegaLiteRenderException (which derives from it)
        var ex = Assert.Throws<VegaLiteRenderException>(() => loader.Load(tree.Root["data"]));

        // Assert
        Assert.Contains("'d.json' contains text that is not valid Unicode", ex.Message, StringComparison.Ordinal);
        Assert.Equal("/data/url", ex.Location.Pointer);
        Assert.Equal(SpecName, ex.Location.File);
    }

    /// <summary>
    ///     Validates that a named dataset that nobody defines is a located failure and not an empty chart, in lenient and
    ///     strict mode alike.
    /// </summary>
    [Fact]
    public void DataLoader_Load_MissingNamedDataIsHardFailure()
    {
        // Arrange
        const string json = """{"data":{"name":"nobody"},"mark":"point"}""";
        var tree = SpecParser.Parse(json, SpecName);

        // Act
        var direct = Assert.Throws<VegaLiteRenderException>(() => CreateLoader(tree).Load(tree.Root["data"]));
        var document = VegaLiteDocument.Parse(json);
        var lenient = Assert.Throws<VegaLiteRenderException>(() => document.Render(cancellationToken: Ct));
        var strict = Assert.Throws<VegaLiteRenderException>(() => document.Render(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true }, Ct));

        // Assert: the failure names the dataset and where to define it, and points at the name
        Assert.Contains("The named data 'nobody' is not defined", direct.Message, StringComparison.Ordinal);
        Assert.Equal("/data/name", direct.Location.Pointer);
        Assert.Equal("/data/name", lenient.Location.Pointer);
        Assert.Equal("/data/name", strict.Location.Pointer);
    }

    /// <summary>
    ///     Validates that a sequence runs from the start up to, but not including, the stop, with the default step and field
    ///     and with a custom step, negative step and field, and that an empty direction gives an empty table.
    /// </summary>
    [Fact]
    public void DataLoader_Load_SequenceUsesExclusiveStopAndAs()
    {
        // Arrange / Act
        var basic = DataLoader.Load(Spec("{\"sequence\":{\"start\":0,\"stop\":3}}").Root["data"], null, null, Ct);
        var named = DataLoader.Load(Spec("{\"sequence\":{\"start\":1,\"stop\":2,\"step\":0.25,\"as\":\"x.y\"}}").Root["data"], null, null, Ct);
        var down = DataLoader.Load(Spec("{\"sequence\":{\"start\":5,\"stop\":1,\"step\":-2}}").Root["data"], null, null, Ct);
        var wrongWay = DataLoader.Load(Spec("{\"sequence\":{\"start\":0,\"stop\":3,\"step\":-1}}").Root["data"], null, null, Ct);
        var drift = DataLoader.Load(Spec("{\"sequence\":{\"start\":0,\"stop\":1,\"step\":0.1}}").Root["data"], null, null, Ct);

        // Assert: start inclusive, stop exclusive, in the field "data"
        Assert.Equal(["data"], basic.Fields);
        Assert.Equal([0.0, 1.0, 2.0], basic.Column(0).Select(v => v.Number));

        // Assert: a custom step and field name (the dot needs the escape to address it)
        Assert.Equal(["x\\.y"], named.Fields);
        Assert.Equal([1.0, 1.25, 1.5, 1.75], named.Column(0).Select(v => v.Number));

        // Assert: a negative step counts down and also excludes the stop; a step in the wrong direction is empty
        Assert.Equal([5.0, 3.0], down.Column(0).Select(v => v.Number));
        Assert.Equal(0, wrongWay.RowCount);

        // Assert: every value is start + index * step (not accumulated), so there are ten values and no drift
        Assert.Equal(10, drift.RowCount);
        Assert.Equal(0 + (3 * 0.1), drift.GetValue(3, 0).Number);
    }

    /// <summary>
    ///     Validates that the count of a sequence is exact when the rounded quotient overshoots: the value the stop
    ///     would be generated as (here 0.30000000000000004) is not part of the sequence, for both signs of the step, and
    ///     that exact multiples keep their count.
    /// </summary>
    [Fact]
    public void DataLoader_Load_SequenceCountNeverIncludesTheStop()
    {
        // Arrange / Act: the quotient is 3.0000000000000004, whose ceiling is 4, but the fourth value equals the stop
        var up = LoadSequenceNode(0, 0.30000000000000004, 0.1);
        var down = LoadSequenceNode(0, -0.30000000000000004, -0.1);
        var multiples = new[] { (0.0, 3.0, 1.0, 3), (0.0, 3.0, 0.5, 6), (3.0, 0.0, -1.0, 3), (0.0, 1.0, 0.25, 4), (0.0, 1.0, 0.1, 10) };

        // Assert: three rows each, all strictly before the stop
        Assert.Equal(3, up.RowCount);
        Assert.All(up.Column(0), v => Assert.True(v.Number < 0.30000000000000004));
        Assert.Equal(3, down.RowCount);
        Assert.All(down.Column(0), v => Assert.True(v.Number > -0.30000000000000004));

        // Assert: exact multiples exclude the stop and keep every value before it
        foreach (var (start, stop, step, count) in multiples)
        {
            var table = LoadSequenceNode(start, stop, step);
            Assert.Equal(count, table.RowCount);
            Assert.All(table.Column(0), v => Assert.True(step > 0 ? v.Number < stop : v.Number > stop));
        }
    }

    /// <summary>
    ///     Validates that the row limit is charged with the corrected count of a sequence.
    /// </summary>
    [Fact]
    public void DataLoader_Load_SequenceLimitIsChargedWithTheCorrectedCount()
    {
        // Arrange: three values fit a limit of three; the overshooting count of four would not
        var limits = new VegaLiteLimits { MaxDataRows = 3 };

        // Act
        var table = DataLoader.Load(Spec("{\"sequence\":{\"start\":0,\"stop\":0.30000000000000004,\"step\":0.1}}").Root["data"], null, limits, Ct);

        // Assert
        Assert.Equal(3, table.RowCount);
    }

    /// <summary>
    ///     Validates that a sequence is checked against the row limit arithmetically, before any row is allocated.
    /// </summary>
    [Fact]
    public void DataLoader_Load_SequenceRowLimitIsCheckedBeforeAllocation()
    {
        // Arrange: a sequence that would need far more memory than any machine has if it were allocated
        var huge = Spec("{\"sequence\":{\"start\":0,\"stop\":1e15}}");
        var small = Spec("{\"sequence\":{\"start\":0,\"stop\":4}}");

        // Act
        var hugeEx = Assert.Throws<VegaLiteRenderException>(() => DataLoader.Load(huge.Root["data"], SpecName, null, Ct));
        var smallEx = Assert.Throws<VegaLiteRenderException>(() => DataLoader.Load(small.Root["data"], SpecName, new VegaLiteLimits { MaxDataRows = 3 }, Ct));

        // Assert: refused with the limit named, at the sequence; exactly the limit is accepted
        Assert.Contains("the limit of 1000000 (MaxDataRows)", hugeEx.Message, StringComparison.Ordinal);
        Assert.Equal("/data/sequence", hugeEx.Location.Pointer);
        Assert.Contains("generated sequence has 4 rows", smallEx.Message, StringComparison.Ordinal);
        Assert.Equal(4, DataLoader.Load(small.Root["data"], SpecName, new VegaLiteLimits { MaxDataRows = 4 }, Ct).RowCount);
    }

    /// <summary>
    ///     Validates that a sequence without a <c>start</c> or a <c>stop</c> is a located failure, never an empty table.
    /// </summary>
    [Fact]
    public void DataLoader_Load_SequenceMissingStartOrStopIsLocatedFailure()
    {
        // Arrange / Act: the loader is exercised directly (the specification parser also refuses these before it)
        var noStart = Assert.Throws<VegaLiteRenderException>(() => LoadSequenceMembers(("stop", 3.0)));
        var noStop = Assert.Throws<VegaLiteRenderException>(() => LoadSequenceMembers(("start", 0.0)));
        var neither = Assert.Throws<VegaLiteRenderException>(() => LoadSequenceMembers());
        var onlyStep = Assert.Throws<VegaLiteRenderException>(() => LoadSequenceMembers(("step", 1.0)));

        // Assert
        foreach (var ex in new[] { noStart, noStop, neither, onlyStep })
        {
            Assert.Contains("needs a finite numeric 'start' and 'stop'", ex.Message, StringComparison.Ordinal);
            Assert.Equal("/data/sequence", ex.Location.Pointer);
            Assert.Equal(SpecName, ex.Location.File);
        }
    }

    /// <summary>
    ///     Validates that a <c>start</c> or <c>stop</c> that is not a number (text, boolean, null, object) or not finite
    ///     is a located failure.
    /// </summary>
    [Fact]
    public void DataLoader_Load_SequenceNonNumericEndpointsAreLocatedFailure()
    {
        // Arrange: values that are not numbers (text, boolean, null, object) and numbers that are not finite
        object?[] bad = ["0", true, null, new object(), double.NaN, double.PositiveInfinity, double.NegativeInfinity];

        // Act / Assert: each is refused as the start and as the stop, located at the sequence
        foreach (var value in bad)
        {
            var start = Assert.Throws<VegaLiteRenderException>(() => LoadSequenceMembers(("start", value), ("stop", 3.0)));
            var stop = Assert.Throws<VegaLiteRenderException>(() => LoadSequenceMembers(("start", 0.0), ("stop", value)));
            foreach (var ex in new[] { start, stop })
            {
                Assert.Contains("needs a finite numeric 'start' and 'stop'", ex.Message, StringComparison.Ordinal);
                Assert.Equal("/data/sequence", ex.Location.Pointer);
            }
        }
    }

    /// <summary>
    ///     Validates that a step of zero, a non-finite step and a step that is present but not a number are located
    ///     failures, while an absent step defaults to 1.
    /// </summary>
    [Fact]
    public void DataLoader_Load_SequenceZeroOrNonFiniteStepIsLocatedFailure()
    {
        // Arrange: a step of zero (or negative zero), non-finite numbers, and values that are present but not numbers
        object?[] bad = [0.0, -0.0, double.NaN, double.PositiveInfinity, double.NegativeInfinity, "1", true, null, new object()];

        // Act / Assert: each is refused (a present step never silently becomes 1), located at the sequence
        foreach (var value in bad)
        {
            var ex = Assert.Throws<VegaLiteRenderException>(() => LoadSequenceMembers(("start", 0.0), ("stop", 3.0), ("step", value)));
            Assert.Contains("step must be a finite number other than zero", ex.Message, StringComparison.Ordinal);
            Assert.Equal("/data/sequence", ex.Location.Pointer);
        }

        // Assert: only an absent step defaults to 1
        Assert.Equal(3, LoadSequenceMembers(("start", 0.0), ("stop", 3.0)).RowCount);
    }

    /// <summary>
    ///     Validates that a step that points away from the stop gives an empty table (not an error), for a positive and a
    ///     negative step, and that equal endpoints are empty too.
    /// </summary>
    [Fact]
    public void DataLoader_Load_SequenceWrongDirectionWithPositiveStepIsEmpty()
    {
        // Arrange / Act
        var positiveStepDownhill = DataLoader.Load(Spec("{\"sequence\":{\"start\":5,\"stop\":1}}").Root["data"], null, null, Ct);
        var explicitPositive = DataLoader.Load(Spec("{\"sequence\":{\"start\":5,\"stop\":1,\"step\":2}}").Root["data"], null, null, Ct);
        var negativeStepUphill = DataLoader.Load(Spec("{\"sequence\":{\"start\":1,\"stop\":5,\"step\":-1}}").Root["data"], null, null, Ct);
        var equal = DataLoader.Load(Spec("{\"sequence\":{\"start\":2,\"stop\":2}}").Root["data"], null, null, Ct);

        // Assert: all are empty tables with the sequence field
        Assert.All(new[] { positiveStepDownhill, explicitPositive, negativeStepUphill, equal }, t => Assert.Equal(0, t.RowCount));
        Assert.Equal(["data"], positiveStepDownhill.Fields);
    }

    /// <summary>
    ///     Validates that arithmetic that overflows (the difference of the endpoints, or the quotient by a tiny step) is
    ///     refused by the row limit before any allocation, as a located failure, and that an overflowing difference in the
    ///     wrong direction is still empty.
    /// </summary>
    [Fact]
    public void DataLoader_Load_SequenceArithmeticOverflowFailsBeforeAllocation()
    {
        // Arrange / Act: stop - start overflows to infinity; the quotient overflows with a tiny step; both directions
        var span = Assert.Throws<VegaLiteRenderException>(() => LoadSequenceNode(-double.MaxValue, double.MaxValue));
        var tiny = Assert.Throws<VegaLiteRenderException>(() => LoadSequenceNode(0, double.MaxValue, double.Epsilon));
        var big = Assert.Throws<VegaLiteRenderException>(() => LoadSequenceNode(0, 1e300, 1e-10));
        var huge = Assert.Throws<VegaLiteRenderException>(() => LoadSequenceNode(0, 1e300));
        var backward = LoadSequenceNode(double.MaxValue, -double.MaxValue);

        // Assert: refused with the limit named at the sequence, with no invalid count in the message
        foreach (var ex in new[] { span, tiny, big, huge })
        {
            Assert.Contains("(MaxDataRows)", ex.Message, StringComparison.Ordinal);
            Assert.Equal("/data/sequence", ex.Location.Pointer);
            Assert.DoesNotContain("Infinity", ex.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("NaN", ex.Message, StringComparison.Ordinal);
        }

        Assert.Equal(0, backward.RowCount);
    }

    /// <summary>
    ///     Loads a sequence built directly as specification nodes, for values the specification parser refuses (it
    ///     validates the numbers of a sequence itself) or cannot represent (non-finite numbers).
    /// </summary>
    /// <param name="start">The <c>start</c>.</param>
    /// <param name="stop">The <c>stop</c>.</param>
    /// <param name="step">The <c>step</c>, or null to leave it out.</param>
    /// <returns>The table.</returns>
    private static DataTable LoadSequenceNode(double start, double stop, double? step = null) =>
        step is { } given
            ? LoadSequenceMembers(("start", start), ("stop", stop), ("step", given))
            : LoadSequenceMembers(("start", start), ("stop", stop));

    /// <summary>
    ///     Loads a sequence whose members are built directly as specification nodes.
    /// </summary>
    /// <param name="members">
    ///     The members: a <see cref="double"/> becomes a number, a <see cref="string"/> a string, a <see cref="bool"/> a
    ///     boolean, <see langword="null"/> a JSON null and anything else an empty object.
    /// </param>
    /// <returns>The table.</returns>
    private static DataTable LoadSequenceMembers(params (string Name, object? Value)[] members)
    {
        var position = new NodePosition(1, 1, 1, 1);
        SpecNode Member(string name, object? value) => value switch
        {
            double number => new SpecNode(SpecNodeKind.Number, name, -1, position, null, number, false, null),
            string text => new SpecNode(SpecNodeKind.String, name, -1, position, text, 0, false, null),
            bool flag => new SpecNode(SpecNodeKind.Boolean, name, -1, position, null, 0, flag, null),
            null => new SpecNode(SpecNodeKind.Null, name, -1, position, null, 0, false, null),
            _ => new SpecNode(SpecNodeKind.Object, name, -1, position, null, 0, false, null),
        };

        var sequence = new SpecNode(SpecNodeKind.Object, "sequence", -1, position, null, 0, false, [.. members.Select(m => Member(m.Name, m.Value))]);
        var data = new SpecNode(SpecNodeKind.Object, "data", -1, position, null, 0, false, [sequence]);

        // The root adopts the data node, so that the pointer of the sequence is /data/sequence
        _ = new SpecNode(SpecNodeKind.Object, null, -1, position, null, 0, false, [data]);
        return DataLoader.Load(data, SpecName, null, Ct);
    }

    /// <summary>
    ///     Validates that the row limit is a total across the tables of one call: the first table fits, a second that
    ///     would pass the total is refused, and the counter reports what was read.
    /// </summary>
    [Fact]
    public void DataLoader_Load_RowLimitIsATotalAcrossTables()
    {
        // Arrange: two tables of three rows each against a limit of five
        var tree = Spec("{\"values\":[{\"a\":1},{\"a\":2},{\"a\":3}]}");
        var loader = new DataLoader(SpecName, new VegaLiteLimits { MaxDataRows = 5 }, cancellationToken: Ct);

        // Act
        var first = loader.Load(tree.Root["data"]);
        var ex = Assert.Throws<VegaLiteRenderException>(() => loader.Load(tree.Root["data"]));

        // Assert
        Assert.Equal(3, first.RowCount);
        Assert.Equal(3, loader.RowsLoaded);
        Assert.Contains("with the 3 rows read before", ex.Message, StringComparison.Ordinal);
        Assert.Contains("the limit of 5 (MaxDataRows)", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that a local file that cannot be found is a located failure, that a document without any file source
    ///     fails the same way, and that neither is an empty dataset.
    /// </summary>
    [Fact]
    public void DataLoader_Load_MissingLocalFileIsHardFailure()
    {
        // Arrange
        var tree = Spec("{\"url\":\"missing.csv\"}");
        var files = new VirtualDataFiles();

        // Act
        var missing = Assert.Throws<VegaLiteRenderException>(() => CreateLoader(tree, Virtual(files)).Load(tree.Root["data"]));
        var noSource = Assert.Throws<VegaLiteRenderException>(() => DataLoader.Load(tree.Root["data"], SpecName, null, Ct));

        // Assert
        Assert.Contains("'missing.csv' was not found", missing.Message, StringComparison.Ordinal);
        Assert.Equal("/data/url", missing.Location.Pointer);
        Assert.Contains("no base directory, data path or file resolver", noSource.Message, StringComparison.Ordinal);
        Assert.Equal(["missing.csv"], files.Requests);
    }

    /// <summary>
    ///     Validates that a file is read once per call however often the chart names it, and that two names for one file
    ///     count as one file for the file limit.
    /// </summary>
    [Fact]
    public void DataLoader_Load_FileIsReadOnceWhenNamedAgain()
    {
        // Arrange
        var files = new VirtualDataFiles().With("a.csv", "x\n1\n2\n");
        var tree = Spec("{\"url\":\"a.csv\"}");
        var loader = CreateLoader(tree, Virtual(files), new VegaLiteLimits { MaxDataFiles = 1 });

        // Act
        var first = loader.Load(tree.Root["data"]);
        var second = loader.Load(tree.Root["data"]);

        // Assert: both tables have the rows, the callback saw one request, and the file limit of one was enough
        Assert.Equal(2, first.RowCount);
        Assert.Equal(2, second.RowCount);
        Assert.Equal(["a.csv"], files.Requests);
        Assert.Equal(4, loader.RowsLoaded);
    }

    /// <summary>
    ///     Validates that values that an explicit parse cannot read are reported as one warning per field, with the
    ///     location of the data, and are null in the table.
    /// </summary>
    [Fact]
    public void DataLoader_Load_ParseFailuresAreCollectedAsWarnings()
    {
        // Arrange
        var files = new VirtualDataFiles().With("d.csv", "n,m\n1,x\nbad,2\nworse,3\n");
        var tree = Spec("{\"url\":\"d.csv\",\"format\":{\"parse\":{\"n\":\"number\"}}}");
        var loader = CreateLoader(tree, Virtual(files));

        // Act
        var table = loader.Load(tree.Root["data"]);

        // Assert: one warning for the field n, none for the field that was not named; the bad cells are null
        var warning = Assert.Single(loader.Warnings);
        Assert.Equal("vl-data-parse-failed", warning.Id);
        Assert.Equal(FeatureReportKind.Warning, warning.Kind);
        Assert.Equal("/data", warning.Location.Pointer);
        Assert.Contains("2 value(s) of the field 'n'", warning.Message, StringComparison.Ordinal);
        Assert.Equal(DataValueKind.Number, table.GetValue(0, "n").Kind);
        Assert.True(table.GetValue(1, "n").IsNull);
        Assert.Equal("x", table.GetValue(0, "m").Text);
    }

    /// <summary>
    ///     Validates that the data of a view is loaded before the scales: a chart whose file is missing fails with the
    ///     location of the <c>url</c>, so a limit or an error never depends on later stages.
    /// </summary>
    [Fact]
    public void DataLoader_Load_DelimitedLimitsAndFailuresCarryTheLocationOfTheUrl()
    {
        // Arrange: a CSV with three rows against a limit of two, and one with a repeated column
        var files = new VirtualDataFiles().With("rows.csv", "a\n1\n2\n3\n").With("dup.csv", "a,a\n1,2\n");
        var rows = Spec("{\"url\":\"rows.csv\"}");
        var dup = Spec("{\"url\":\"dup.csv\"}");

        // Act
        var tooMany = Assert.Throws<VegaLiteRenderException>(
            () => CreateLoader(rows, Virtual(files), new VegaLiteLimits { MaxDataRows = 2 }).Load(rows.Root["data"]));
        var repeated = Assert.Throws<VegaLiteRenderException>(() => CreateLoader(dup, Virtual(files)).Load(dup.Root["data"]));

        // Assert
        Assert.Contains("MaxDataRows", tooMany.Message, StringComparison.Ordinal);
        Assert.Equal("/data/url", tooMany.Location.Pointer);
        Assert.Equal(1, tooMany.Location.Line);
        Assert.Contains("more than once", repeated.Message, StringComparison.Ordinal);
    }
}
