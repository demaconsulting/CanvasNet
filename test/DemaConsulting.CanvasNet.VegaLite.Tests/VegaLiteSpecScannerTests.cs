namespace DemaConsulting.CanvasNet.VegaLite.Tests;

/// <summary>
///     Unit tests for <see cref="VegaLiteSpecScanner"/>, the foundation reader of specification metadata.
/// </summary>
public class VegaLiteSpecScannerTests
{
    /// <summary>
    ///     Scans text with the default depth and no source name.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="name">An optional source name.</param>
    /// <param name="depth">The maximum depth.</param>
    /// <returns>The scan result.</returns>
    private static VegaLiteSpecInfo Scan(string json, string? name = null, int depth = 64) =>
        new VegaLiteSpecScanner(VegaLiteSpecScanner.Encode(json), name, depth).Scan();

    /// <summary>
    ///     Validates that an empty object yields empty metadata.
    /// </summary>
    [Fact]
    public void VegaLiteSpecScanner_Scan_EmptyObject_YieldsEmptyMetadata()
    {
        // Arrange / Act
        var info = Scan("{}");

        // Assert
        Assert.Null(info.SchemaUrl);
        Assert.Null(info.SchemaMajorVersion);
        Assert.Null(info.Title);
        Assert.Empty(info.Marks);
        Assert.Empty(info.DataReferences);
        Assert.Empty(info.ParameterNames);
    }

    /// <summary>
    ///     Validates the schema major version for the supported URL forms.
    /// </summary>
    /// <param name="url">The schema URL.</param>
    /// <param name="expected">The expected major version, or -1 for none.</param>
    [Theory]
    [InlineData("https://vega.github.io/schema/vega-lite/v5.json", 5)]
    [InlineData("https://vega.github.io/schema/vega-lite/v6.json", 6)]
    [InlineData("https://vega.github.io/schema/vega-lite/v5.17.0.json", 5)]
    [InlineData("https://vega.github.io/schema/vega-lite/v12.json", 12)]
    [InlineData("https://example.org/schema/vega-lite/V4.json", 4)]
    [InlineData("https://example.org/schema/other.json", -1)]
    [InlineData("https://vega.github.io/schema/vega-lite/v.json", -1)]
    [InlineData("https://vega.github.io/schema/vega-lite/v99999.json", -1)]
    public void VegaLiteSpecScanner_Scan_Schema_ParsesMajorVersion(string url, int expected)
    {
        // Arrange
        var json = "{\"$schema\":\"" + url + "\"}";

        // Act
        var info = Scan(json);

        // Assert
        Assert.Equal(url, info.SchemaUrl);
        Assert.Equal(expected < 0 ? null : expected, info.SchemaMajorVersion);
        Assert.Equal("/$schema", info.SchemaLocation.Pointer);
    }

    /// <summary>
    ///     Validates that malformed or plain-Vega schema values are specification errors with a pointer.
    /// </summary>
    /// <param name="json">The specification text.</param>
    [Theory]
    [InlineData("{\"$schema\": 5}")]
    [InlineData("{\"$schema\": \"\"}")]
    [InlineData("{\"$schema\": \"  \"}")]
    [InlineData("{\"$schema\": null}")]
    [InlineData("{\"$schema\": \"https://vega.github.io/schema/vega/v5.json\"}")]
    public void VegaLiteSpecScanner_Scan_BadSchema_ThrowsWithPointer(string json)
    {
        // Arrange / Act
        var ex = Assert.Throws<VegaLiteSpecException>(() => Scan(json));

        // Assert
        Assert.Equal("/$schema", ex.Location.Pointer);
        Assert.Equal(1, ex.Location.Line);
        Assert.Equal(13, ex.Location.Column);
    }

    /// <summary>
    ///     Validates the supported title forms.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="expected">The expected title, or null.</param>
    [Theory]
    [InlineData("{\"title\":\"Sales\"}", "Sales")]
    [InlineData("{\"title\":[\"A\",\"B\"]}", "A\nB")]
    [InlineData("{\"title\":{\"text\":\"T\",\"subtitle\":\"S\"}}", "T")]
    [InlineData("{\"title\":{\"text\":[\"A\",\"B\"]}}", "A\nB")]
    [InlineData("{\"title\":{\"subtitle\":\"S\"}}", null)]
    [InlineData("{\"title\":{\"text\":{\"expr\":\"x\"}}}", null)]
    [InlineData("{\"title\":null}", null)]
    public void VegaLiteSpecScanner_Scan_Title_ReadsPlainText(string json, string? expected)
    {
        // Arrange / Act
        var info = Scan(json);

        // Assert
        Assert.Equal(expected, info.Title);
    }

    /// <summary>
    ///     Validates mark discovery in document order, including nested views and mark definition objects.
    /// </summary>
    [Fact]
    public void VegaLiteSpecScanner_Scan_Marks_AreFoundInDocumentOrderWithPointers()
    {
        // Arrange
        const string json = """
            {
              "layer": [
                { "mark": "bar" },
                { "mark": { "type": "point", "filled": true } },
                { "layer": [ { "mark": "rule" } ] }
              ],
              "mark": "text",
              "vconcat": [ { "mark": "line" } ],
              "spec": { "mark": "area" },
              "encoding": { "x": { "mark": "ignored" } }
            }
            """;

        // Act
        var info = Scan(json);

        // Assert
        Assert.Equal(["bar", "point", "rule", "text", "line", "area"], info.Marks.Select(m => m.Type));
        Assert.Equal(
            ["/layer/0/mark", "/layer/1/mark", "/layer/2/layer/0/mark", "/mark", "/vconcat/0/mark", "/spec/mark"],
            info.Marks.Select(m => m.Location.Pointer));
        Assert.Equal((3, 15), (info.Marks[0].Location.Line, info.Marks[0].Location.Column));
    }

    /// <summary>
    ///     Validates that invalid title, mark, composition and data shapes are specification errors.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="pointer">The expected pointer.</param>
    [Theory]
    [InlineData("{\"title\":5}", "/title")]
    [InlineData("{\"title\":true}", "/title")]
    [InlineData("{\"title\":[\"A\",3]}", "/title/1")]
    [InlineData("{\"title\":{\"text\":7}}", "/title/text")]
    [InlineData("{\"mark\":3}", "/mark")]
    [InlineData("{\"mark\":{}}", "/mark")]
    [InlineData("{\"mark\":{\"type\":3}}", "/mark/type")]
    [InlineData("{\"layer\":{}}", "/layer")]
    [InlineData("{\"layer\":[1]}", "/layer/0")]
    [InlineData("{\"spec\":[]}", "/spec")]
    [InlineData("{\"data\":5}", "/data")]
    [InlineData("{\"data\":{\"url\":3}}", "/data/url")]
    public void VegaLiteSpecScanner_Scan_BadShapes_ThrowWithPointer(string json, string pointer)
    {
        // Arrange / Act
        var ex = Assert.Throws<VegaLiteSpecException>(() => Scan(json));

        // Assert
        Assert.Equal(pointer, ex.Location.Pointer);
    }

    /// <summary>
    ///     Validates data reference discovery for every kind, including row counts.
    /// </summary>
    [Fact]
    public void VegaLiteSpecScanner_Scan_Data_ListsReferencesWithRowCounts()
    {
        // Arrange
        const string json = """
            {
              "data": { "values": [ {"a":1}, {"a":2}, [1,2,3] ] },
              "layer": [
                { "data": { "url": "points.csv", "format": { "type": "csv" } } },
                { "data": { "name": "table" } },
                { "data": { "sequence": { "start": 0, "stop": 10, "step": 2 } } },
                { "data": { "values": [] } },
                { "data": { "values": "a,b\n1,2" } },
                { "data": null },
                { "data": { "format": {} } },
                {}
              ]
            }
            """;

        // Act
        var references = Scan(json).DataReferences;

        // Assert
        Assert.Equal(
            [VegaLiteDataKind.Inline, VegaLiteDataKind.Url, VegaLiteDataKind.Name, VegaLiteDataKind.Sequence, VegaLiteDataKind.Inline, VegaLiteDataKind.Inline],
            references.Select(r => r.Kind));
        Assert.Equal([3L, null, null, 5L, 0L, null], references.Select(r => r.RowCount));
        Assert.Equal("points.csv", references[1].Url);
        Assert.Equal(["/data", "/layer/0/data", "/layer/1/data", "/layer/2/data", "/layer/3/data", "/layer/4/data"], references.Select(r => r.Location.Pointer));
        Assert.Equal((2, 11), (references[0].Location.Line, references[0].Location.Column));
    }

    /// <summary>
    ///     Validates the computed length of sequences, including degenerate ones.
    /// </summary>
    /// <param name="sequence">The sequence object text.</param>
    /// <param name="expected">The expected length, or -1 when unknown.</param>
    [Theory]
    [InlineData("{\"start\":0,\"stop\":5}", 5)]
    [InlineData("{\"start\":0,\"stop\":1,\"step\":0.25}", 4)]
    [InlineData("{\"start\":5,\"stop\":0}", 0)]
    [InlineData("{\"start\":5,\"stop\":0,\"step\":-1}", 5)]
    [InlineData("{\"start\":0,\"stop\":5,\"step\":0}", -1)]
    [InlineData("{\"start\":0}", -1)]
    [InlineData("{\"start\":\"a\",\"stop\":3}", -1)]
    [InlineData("{\"start\":0,\"stop\":1e300}", -1)]
    public void VegaLiteSpecScanner_Scan_Sequence_ComputesLength(string sequence, long expected)
    {
        // Arrange
        var json = "{\"data\":{\"sequence\":" + sequence + "}}";

        // Act
        var reference = Assert.Single(Scan(json).DataReferences);

        // Assert
        Assert.Equal(expected < 0 ? null : expected, reference.RowCount);
    }

    /// <summary>
    ///     Validates parameter name discovery: document order, distinct names, nested views.
    /// </summary>
    [Fact]
    public void VegaLiteSpecScanner_Scan_Params_AreDistinctInDocumentOrder()
    {
        // Arrange
        const string json = """
            {
              "params": [ { "name": "b", "value": 1 }, { "name": "a" }, 7, { "value": 2 } ],
              "layer": [ { "params": [ { "name": "a" }, { "name": "c" } ] } ]
            }
            """;

        // Act
        var info = Scan(json);

        // Assert
        Assert.Equal(["b", "a", "c"], info.ParameterNames);
    }

    /// <summary>
    ///     Validates that columns count UTF-16 code units: tabs are one column and astral characters are two.
    /// </summary>
    [Fact]
    public void VegaLiteSpecScanner_Scan_Locations_CountUtf16CodeUnits()
    {
        // Arrange: a tab, then an astral character (a surrogate pair in UTF-16) inside a string before the mark
        var json = "{\r\n\t\"note\": \"\U0001F600\", \"mark\": \"bar\"\r\n}";

        // Act
        var info = Scan(json, "x.json");

        // Assert: line 2; tab (1) + "note" (6) + ": " (2) + string of 4 units (quote, 2, quote) + ", " (2) + "mark" (6) + ": " (2)
        var location = Assert.Single(info.Marks).Location;
        Assert.Equal("x.json", location.File);
        Assert.Equal(2, location.Line);
        Assert.Equal(1 + 6 + 2 + 4 + 2 + 6 + 2 + 1, location.Column);
    }

    /// <summary>
    ///     Validates the location of malformed JSON.
    /// </summary>
    [Fact]
    public void VegaLiteSpecScanner_Scan_MalformedJson_ReportsLineAndColumn()
    {
        // Arrange
        const string json = "{\n  \"mark\": bar\n}";

        // Act
        var ex = Assert.Throws<VegaLiteSpecException>(() => Scan(json, "bad.json"));

        // Assert
        Assert.Equal("bad.json", ex.Location.File);
        Assert.Equal(2, ex.Location.Line);
        Assert.Equal(11, ex.Location.Column);
        Assert.StartsWith("bad.json:2:11: Malformed JSON: ", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("LineNumber", ex.Message, StringComparison.Ordinal);
        Assert.IsAssignableFrom<System.Text.Json.JsonException>(ex.InnerException);
    }

    /// <summary>
    ///     Validates the other malformed inputs: empty text, non-object root and trailing content.
    /// </summary>
    /// <param name="json">The text.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    [InlineData("{} x")]
    [InlineData("{}{}")]
    [InlineData("{\"a\": 1,}")]
    [InlineData("{\"a\": // c\n 1}")]
    [InlineData("{\"a\": \"unterminated}")]
    public void VegaLiteSpecScanner_Scan_NotASpecification_ThrowsSpecException(string json)
    {
        // Arrange / Act
        var ex = Assert.Throws<VegaLiteSpecException>(() => Scan(json));

        // Assert
        Assert.True(ex.Location.Line >= 1);
        Assert.True(ex.Location.Column >= 1);
    }

    /// <summary>
    ///     Validates that nesting beyond the limit fails closed at the location where it is exceeded.
    /// </summary>
    [Fact]
    public void VegaLiteSpecScanner_Scan_TooDeep_ThrowsWithLocation()
    {
        // Arrange
        var json = "{\"a\":[[[[1]]]]}";

        // Act
        var ex = Assert.Throws<VegaLiteSpecException>(() => Scan(json, null, 3));

        // Assert
        Assert.Contains("deeper than the limit of 3", ex.Message, StringComparison.Ordinal);
        Assert.Equal(1, ex.Location.Line);
    }

    /// <summary>
    ///     Validates that very deep nesting of views fails closed instead of overflowing the stack.
    /// </summary>
    [Fact]
    public void VegaLiteSpecScanner_Scan_DeepViewNesting_FailsClosed()
    {
        // Arrange: far deeper than the maximum accepted depth of 256
        var json = string.Concat(Enumerable.Repeat("{\"layer\":[", 5000)) + "{}" + string.Concat(Enumerable.Repeat("]}", 5000));

        // Act / Assert
        Assert.Throws<VegaLiteSpecException>(() => Scan(json, null, 256));
    }

    /// <summary>
    ///     Validates that unknown properties are skipped but still validated as JSON.
    /// </summary>
    [Fact]
    public void VegaLiteSpecScanner_Scan_UnknownProperties_AreSkippedButValidated()
    {
        // Arrange / Act
        var info = Scan("{\"config\":{\"a\":[1,{\"b\":null}]},\"mark\":\"bar\"}");

        // Assert
        Assert.Equal("bar", Assert.Single(info.Marks).Type);
        Assert.Throws<VegaLiteSpecException>(() => Scan("{\"config\":{\"a\":[1,}},\"mark\":\"bar\"}"));
    }

    /// <summary>
    ///     Validates the escaping of pointer segments.
    /// </summary>
    [Fact]
    public void VegaLiteSpecScanner_EscapePointerSegment_EscapesTildeAndSlash()
    {
        // Arrange / Act / Assert
        Assert.Equal("a~1b~0c", VegaLiteSpecScanner.EscapePointerSegment("a/b~c"));
        Assert.Equal("plain", VegaLiteSpecScanner.EscapePointerSegment("plain"));
    }

    /// <summary>
    ///     Validates which data URLs count as remote or scheme-bearing.
    /// </summary>
    /// <param name="url">The URL.</param>
    /// <param name="expected">Whether it is scheme-bearing.</param>
    [Theory]
    [InlineData("http://example.org/a.csv", true)]
    [InlineData("HTTPS://example.org/a.csv", true)]
    [InlineData("file:///etc/passwd", true)]
    [InlineData("data:text/csv,a", true)]
    [InlineData("javascript:alert(1)", true)]
    [InlineData("ftp://host/a.csv", true)]
    [InlineData("C:\\data\\a.csv", true)]
    [InlineData("//server/share/a.csv", true)]
    [InlineData("\\\\server\\share\\a.csv", true)]
    [InlineData("  http://example.org/a.csv", true)]
    [InlineData("a.csv", false)]
    [InlineData("data/a.csv", false)]
    [InlineData("../a.csv", false)]
    [InlineData("a b.csv", false)]
    [InlineData("", false)]
    [InlineData("1:2.csv", false)]
    public void VegaLiteSpecScanner_IsSchemeBearingUrl_ClassifiesUrls(string url, bool expected)
    {
        // Arrange / Act / Assert
        Assert.Equal(expected, VegaLiteSpecScanner.IsSchemeBearingUrl(url));
    }
}
