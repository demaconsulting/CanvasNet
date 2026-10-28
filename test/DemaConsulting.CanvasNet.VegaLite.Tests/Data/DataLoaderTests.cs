using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Data;

/// <summary>
///     Tests of the inline data loader: the rows it reads, the sources it never reads, and its limits. Where the feature
///     catalog is responsible for a report, the test also checks the report through <see cref="VegaLiteDocument"/>.
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
    ///     Validates that every other kind of source is an empty table and is reported by the catalog.
    /// </summary>
    [Theory]
    [InlineData("""{"url":"rows.json"}""", "vl-data-file")]
    [InlineData("""{"name":"table"}""", "vl-data-named")]
    [InlineData("""{"sequence":{"start":0,"stop":3}}""", "vl-data-sequence")]
    [InlineData("""{"values":"a,b\n1,2"}""", "vl-data-values-text")]
    [InlineData("""{"url":"\\\\server\\share\\rows.json"}""", "vl-data-remote-url")]
    public void DataLoader_Load_OtherSourcesAreEmptyTablesReportedByTheCatalog(string dataJson, string expectedFeature)
    {
        // Arrange
        var json = "{\"data\":" + dataJson + ",\"mark\":\"point\"}";
        var tree = SpecParser.Parse(json);

        // Act
        var table = DataLoader.Load(tree.Root["data"], null, null, Ct);
        var features = FeatureCatalog.Visit(tree).Select(r => r.Id).ToList();

        // Assert
        Assert.Same(DataTable.Empty, table);
        Assert.Contains(expectedFeature, features);
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
}
