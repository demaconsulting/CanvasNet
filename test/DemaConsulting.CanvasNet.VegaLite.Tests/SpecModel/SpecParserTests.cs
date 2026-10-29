using System.Text;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.SpecModel;

/// <summary>
///     Unit tests for <see cref="SpecParser"/> and the <see cref="SpecNode"/> tree and <see cref="SpecMetadata"/> it feeds.
/// </summary>
public class SpecParserTests
{
    /// <summary>An original specification with inline data, written over several lines so that positions are checkable.</summary>
    private const string InlineSpec = """
        {
          "$schema": "https://vega.github.io/schema/vega-lite/v5.json",
          "title": "Widgets",
          "data": { "values": [ { "kind": "a", "count": 2 }, { "kind": "b", "count": 5.5 } ] },
          "mark": "bar",
          "encoding": {
            "x": { "field": "kind", "type": "nominal" },
            "y": { "field": "count", "type": "quantitative" }
          }
        }
        """;

    /// <summary>
    ///     Validates that a valid inline specification becomes a tree whose nodes carry kind, value, JSON pointer and
    ///     one-based line and column, with properties and elements in document order, and that pointers escape
    ///     <c>~</c> and <c>/</c>.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_ValidInlineSpecRecordsJsonPointerAndLineColumn()
    {
        // Arrange / Act
        var tree = SpecParser.Parse(InlineSpec, "widgets.vl.json");
        var root = tree.Root;
        var rows = root["data"]!["values"]!;
        var second = rows.Items[1];
        var yType = root["encoding"]!["y"]!["type"]!;

        // Assert: the root, the kinds and the properties in document order
        Assert.Equal(SpecNodeKind.Object, root.Kind);
        Assert.Equal(string.Empty, root.Pointer);
        Assert.Equal(["$schema", "title", "data", "mark", "encoding"], root.Properties.Select(p => p.Name));
        Assert.Equal("Widgets", root["title"]!.StringValue);
        Assert.Equal(SpecNodeKind.Array, rows.Kind);
        Assert.Equal(2, rows.Items.Count);
        Assert.Equal(5.5, second["count"]!.NumberValue);
        Assert.Equal("quantitative", yType.StringValue);
        Assert.Null(root["missing"]);
        Assert.Null(root["title"]!["x"]);

        // Assert: RFC 6901 pointers, including array indexes
        Assert.Equal("/data/values/1/count", second["count"]!.Pointer);
        Assert.Equal("/encoding/y/type", yType.Pointer);
        Assert.Equal("/encoding/y", yType.Parent!.Pointer);

        // Assert: one-based lines and columns of the first character of each value
        Assert.Equal((2, 14), (root["$schema"]!.Line, root["$schema"]!.Column));
        Assert.Equal((5, 11), (root["mark"]!.Line, root["mark"]!.Column));
        Assert.Equal((7, 10), (root["encoding"]!["x"]!.Line, root["encoding"]!["x"]!.Column));
        Assert.Equal((1, 1), (root.Line, root.Column));

        // Assert: a property remembers the position of its key as well, and a location carries the source name
        Assert.Equal((5, 3), (root["mark"]!.KeyLine, root["mark"]!.KeyColumn));
        var location = tree.Locate(yType);
        Assert.Equal("widgets.vl.json", location.File);
        Assert.Equal("/encoding/y/type", location.Pointer);
        Assert.Equal((8, 38), (location.Line, location.Column));
        Assert.Equal((8, 30), (tree.LocateKey(yType).Line, tree.LocateKey(yType).Column));

        // Assert: pointer segments are escaped
        var escaped = SpecParser.Parse("{\"config\":{\"a/b\":{\"c~d\":1}}}");
        Assert.Equal("/config/a~1b/c~0d", escaped.Root["config"]!["a/b"]!["c~d"]!.Pointer);
        Assert.Equal("a~1b~0", SpecParser.EscapePointerSegment("a/b~"));
        Assert.Equal("plain", SpecParser.EscapePointerSegment("plain"));
    }

    /// <summary>
    ///     Validates the value kinds and the byte-order-mark and byte-input tolerance of the parser.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_AllValueKindsAndBomAreHandled()
    {
        // Arrange
        const string json = "{\"s\":\"x\",\"n\":-1.5e2,\"t\":true,\"f\":false,\"z\":null,\"a\":[],\"o\":{}}";
        var bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetPreamble();

        // Act
        var fromString = SpecParser.Parse("\uFEFF" + json);
        var fromBytes = SpecParser.Parse([.. bom, .. Encoding.UTF8.GetBytes(json)]);
        var root = fromString.Root;

        // Assert: every kind
        Assert.Equal(SpecNodeKind.String, root["s"]!.Kind);
        Assert.Equal(-150, root["n"]!.NumberValue);
        Assert.True(root["t"]!.BoolValue);
        Assert.True(root["t"]!.IsBoolean);
        Assert.False(root["f"]!.BoolValue);
        Assert.True(root["z"]!.IsNull);
        Assert.True(root["a"]!.IsArray);
        Assert.Empty(root["a"]!.Items);
        Assert.True(root["o"]!.IsObject);
        Assert.Empty(root["o"]!.Properties);
        Assert.Null(root["s"]!.AsNumber());
        Assert.Null(root["n"]!.AsString());
        Assert.Null(root["s"]!.AsBool());
        Assert.Equal("x", root["s"]!.AsString());
        Assert.Equal(-150, root["n"]!.AsNumber());
        Assert.True(root["t"]!.AsBool());

        // Assert: a byte-order mark does not shift columns, whether the text came as a string or as bytes
        Assert.Equal((1, 1), (root.Line, root.Column));
        Assert.Equal((root["s"]!.Line, root["s"]!.Column), (fromBytes.Root["s"]!.Line, fromBytes.Root["s"]!.Column));
        Assert.Equal(6, root["s"]!.Column);
    }

    /// <summary>
    ///     Validates that columns count UTF-16 code units: tabs are one column and astral characters are two.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_LocationsCountUtf16CodeUnits()
    {
        // Arrange: a tab, then an astral character (a surrogate pair in UTF-16) inside a string before the mark
        var json = "{\r\n\t\"note\": \"\U0001F600\", \"mark\": \"bar\"\r\n}";

        // Act
        var tree = SpecParser.Parse(json, "x.json");

        // Assert: line 2; tab (1) + "note" (6) + ": " (2) + string of 4 units (quote, 2, quote) + ", " (2) + "mark" (6) + ": " (2)
        var mark = tree.Root["mark"]!;
        Assert.Equal(2, mark.Line);
        Assert.Equal(1 + 6 + 2 + 4 + 2 + 6 + 2 + 1, mark.Column);
        Assert.Equal("x.json", tree.Locate(mark).File);
    }

    /// <summary>
    ///     Validates that malformed JSON is reported at the first problem with its file, line and column.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_MalformedJsonReportsFirstLocation()
    {
        // Arrange: the first problem is the bare word on line 2; a second problem follows on line 3
        const string json = "{\n  \"mark\": bar,\n  \"data\": ,\n}";

        // Act
        var ex = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse(json, "bad.json"));

        // Assert
        Assert.Equal("bad.json", ex.Location.File);
        Assert.Equal(2, ex.Location.Line);
        Assert.Equal(11, ex.Location.Column);
        Assert.StartsWith("bad.json:2:11: Malformed JSON: ", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("LineNumber", ex.Message, StringComparison.Ordinal);
        Assert.IsAssignableFrom<System.Text.Json.JsonException>(ex.InnerException);
    }

    /// <summary>
    ///     Validates the other malformed inputs: empty text, a non-object root and trailing content.
    /// </summary>
    /// <param name="json">The text.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("[]")]
    [InlineData("\"text\"")]
    [InlineData("{} x")]
    [InlineData("{}{}")]
    [InlineData("{} {}")]
    [InlineData("{}1")]
    [InlineData("{}\n[]")]
    [InlineData("{\"a\": 1,}")]
    [InlineData("{\"a\": // c\n 1}")]
    [InlineData("{\"a\": \"unterminated}")]
    public void SpecParser_Parse_NotASpecificationThrowsLocatedSpecException(string json)
    {
        // Arrange / Act
        var ex = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse(json));

        // Assert
        Assert.True(ex.Location.Line >= 1);
        Assert.True(ex.Location.Column >= 1);
    }

    /// <summary>
    ///     Validates that a second JSON value after the specification is rejected at its own position, and that trailing
    ///     whitespace is fine.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_TrailingValueIsRejectedAtItsLocationButWhitespaceIsAccepted()
    {
        // Arrange / Act
        var ex = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse("{}\n  {}", "t.json"));
        var spaced = SpecParser.Parse("{}  ");
        var lines = SpecParser.Parse("{}\r\n\r\n");

        // Assert: the second value starts at line 2, column 3 of the named source
        Assert.Equal(2, ex.Location.Line);
        Assert.Equal(3, ex.Location.Column);
        Assert.Equal("t.json", ex.Location.File);
        Assert.NotNull(spaced.Root);
        Assert.NotNull(lines.Root);
    }

    /// <summary>
    ///     Validates that bytes that are not valid UTF-8 fail with a specification exception rather than another type.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_InvalidUtf8BytesThrowSpecException()
    {
        // Arrange: 0xFF can never appear in UTF-8
        byte[] bytes = [.. "{\"title\":\"a"u8, 0xFF, .. "\"}"u8];

        // Act / Assert
        Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse(bytes));
    }

    /// <summary>
    ///     Validates that every kind of malformed UTF-8 in a string value is rejected at the byte that starts the text.
    /// </summary>
    /// <param name="hex">The bytes placed between the quotes of a string value, as hex.</param>
    [Theory]
    [InlineData("C0 80")] // overlong NUL
    [InlineData("E0 80 80")] // overlong three-byte encoding of U+0000
    [InlineData("F0 80 80 80")] // overlong four-byte encoding
    [InlineData("C1 BF")] // overlong two-byte encoding of U+007F
    [InlineData("ED A0 80")] // an encoded high surrogate (U+D800)
    [InlineData("ED BF BF")] // an encoded low surrogate (U+DFFF)
    [InlineData("F4 90 80 80")] // above U+10FFFF
    [InlineData("80")] // a stray continuation byte
    [InlineData("C3")] // a truncated two-byte sequence
    [InlineData("E2 82")] // a truncated three-byte sequence
    [InlineData("F0 9F 98")] // a truncated four-byte sequence
    [InlineData("FE")] // never valid
    public void SpecParser_Parse_MalformedUtf8InStringIsRejectedNotReplaced(string hex)
    {
        // Arrange
        byte[] bad = Convert.FromHexString(hex.Replace(" ", string.Empty, StringComparison.Ordinal));
        byte[] bytes = [.. "{\"title\":\"a"u8, .. bad, .. "\"}"u8];

        // Act
        var ex = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse(bytes, "bad.json"));

        // Assert
        Assert.Contains("UTF-8", ex.Message, StringComparison.Ordinal);
        Assert.Equal("bad.json", ex.Location.File);
        Assert.Equal(1, ex.Location.Line);
    }

    /// <summary>
    ///     Validates that malformed UTF-8 in a property name or outside a string is rejected too.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_MalformedUtf8InNameOrStructureIsRejected()
    {
        // Arrange
        byte[] inName = [.. "{\"ti"u8, 0xC0, 0x80, .. "tle\":\"a\"}"u8];
        byte[] inStructure = [.. "{\"title\":"u8, 0xFF, .. "\"a\"}"u8];

        // Act / Assert
        Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse(inName));
        Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse(inStructure));
    }

    /// <summary>
    ///     Validates that well-formed multi-byte UTF-8 (two, three and four bytes) is accepted unchanged, with and
    ///     without a byte-order mark.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_ValidMultiByteUtf8IsAccepted()
    {
        // Arrange: é (2 bytes), € (3 bytes) and an emoji (4 bytes)
        byte[] body = [.. "{\"title\":\"\u00e9\u20ac\U0001F600\"}"u8];
        byte[] withMark = [0xEF, 0xBB, 0xBF, .. body];

        // Act
        var plain = SpecParser.Parse(body);
        var marked = SpecParser.Parse(withMark);

        // Assert
        Assert.Equal("\u00e9\u20ac\U0001F600", plain.Root["title"]!.StringValue);
        Assert.Equal("\u00e9\u20ac\U0001F600", marked.Root["title"]!.StringValue);
    }

    /// <summary>
    ///     Validates that text with an unpaired UTF-16 surrogate is rejected at its line and column, while a valid
    ///     surrogate pair is accepted.
    /// </summary>
    /// <param name="surrogate">The unpaired surrogate.</param>
    [Theory]
    [InlineData('\uD800')]
    [InlineData((char)0xDBFF)]
    [InlineData('\uDC00')]
    [InlineData((char)0xDFFF)]
    public void SpecParser_Parse_UnpairedSurrogateInStringIsRejected(char surrogate)
    {
        // Arrange: the surrogate is the 10th UTF-16 unit of line 2 (a pair before it counts as two units)
        var text = "{\n  \"a\":\"\U0001F600" + surrogate + "\"}";

        // Act
        var ex = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse(text, "s.json"));

        // Assert
        Assert.Contains("surrogate", ex.Message, StringComparison.Ordinal);
        Assert.Equal((2, 10), (ex.Location.Line, ex.Location.Column));
        Assert.Equal("s.json", ex.Location.File);
        Assert.NotNull(SpecParser.Parse("{\"a\":\"\U0001F600\"}"));
    }

    /// <summary>
    ///     Validates that a known property with the wrong type or value is a specification error whose pointer names
    ///     the offending value.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="pointer">The expected pointer.</param>
    [Theory]
    [InlineData("{\"$schema\": 5}", "/$schema")]
    [InlineData("{\"$schema\": \"\"}", "/$schema")]
    [InlineData("{\"$schema\": \"  \"}", "/$schema")]
    [InlineData("{\"$schema\": null}", "/$schema")]
    [InlineData("{\"$schema\": \"https://vega.github.io/schema/vega/v5.json\"}", "/$schema")]
    [InlineData("{\"title\":5}", "/title")]
    [InlineData("{\"title\":true}", "/title")]
    [InlineData("{\"title\":[\"A\",3]}", "/title/1")]
    [InlineData("{\"title\":{\"text\":7}}", "/title/text")]
    [InlineData("{\"title\":{\"text\":[\"a\",null]}}", "/title/text/1")]
    [InlineData("{\"title\":{\"anchor\":\"left\"}}", "/title/anchor")]
    [InlineData("{\"title\":{\"orient\":3}}", "/title/orient")]
    [InlineData("{\"description\":3}", "/description")]
    [InlineData("{\"name\":[]}", "/name")]
    [InlineData("{\"width\":\"wide\"}", "/width")]
    [InlineData("{\"height\":{\"step\":\"x\"}}", "/height/step")]
    [InlineData("{\"height\":{}}", "/height")]
    [InlineData("{\"width\":true}", "/width")]
    [InlineData("{\"padding\":\"5\"}", "/padding")]
    [InlineData("{\"padding\":{\"left\":\"1\"}}", "/padding/left")]
    [InlineData("{\"autosize\":\"stretch\"}", "/autosize")]
    [InlineData("{\"autosize\":{\"type\":\"pad\",\"resize\":1}}", "/autosize/resize")]
    [InlineData("{\"background\":5}", "/background")]
    [InlineData("{\"config\":[]}", "/config")]
    [InlineData("{\"datasets\":5}", "/datasets")]
    [InlineData("{\"datasets\":{\"t\":null}}", "/datasets/t")]
    [InlineData("{\"mark\":3}", "/mark")]
    [InlineData("{\"mark\":{}}", "/mark")]
    [InlineData("{\"mark\":{\"type\":3}}", "/mark/type")]
    [InlineData("{\"mark\":\"blob\"}", "/mark")]
    [InlineData("{\"mark\":{\"type\":\"bar\",\"orient\":\"diagonal\"}}", "/mark/orient")]
    [InlineData("{\"mark\":{\"type\":\"bar\",\"size\":\"big\"}}", "/mark/size")]
    [InlineData("{\"layer\":{}}", "/layer")]
    [InlineData("{\"layer\":[1]}", "/layer/0")]
    [InlineData("{\"layer\":[{\"mark\":3}]}", "/layer/0/mark")]
    [InlineData("{\"vconcat\":[{\"hconcat\":[{\"mark\":3}]}]}", "/vconcat/0/hconcat/0/mark")]
    [InlineData("{\"spec\":[]}", "/spec")]
    [InlineData("{\"data\":5}", "/data")]
    [InlineData("{\"data\":{\"url\":3}}", "/data/url")]
    [InlineData("{\"data\":{\"values\":5}}", "/data/values")]
    [InlineData("{\"data\":{\"format\":{\"type\":\"xml\"}}}", "/data/format/type")]
    [InlineData("{\"data\":{\"sequence\":{\"start\":0}}}", "/data/sequence")]
    [InlineData("{\"data\":{\"sequence\":{\"start\":\"a\",\"stop\":3}}}", "/data/sequence/start")]
    [InlineData("{\"data\":{\"sequence\":{\"start\":0,\"stop\":3,\"step\":\"x\"}}}", "/data/sequence/step")]
    [InlineData("{\"data\":{\"sequence\":{\"start\":0,\"stop\":3,\"as\":\"\"}}}", "/data/sequence/as")]
    [InlineData("{\"data\":{\"format\":{\"delimiter\":\"ab\"}}}", "/data/format/delimiter")]
    [InlineData("{\"data\":{\"format\":{\"delimiter\":\"\"}}}", "/data/format/delimiter")]
    [InlineData("{\"data\":{\"format\":{\"delimiter\":\"\\\"\"}}}", "/data/format/delimiter")]
    [InlineData("{\"data\":{\"format\":{\"delimiter\":\"\\n\"}}}", "/data/format/delimiter")]
    [InlineData("{\"data\":{\"format\":{\"delimiter\":3}}}", "/data/format/delimiter")]
    [InlineData("{\"data\":{\"format\":{\"parse\":\"manual\"}}}", "/data/format/parse")]
    [InlineData("{\"data\":{\"format\":{\"parse\":5}}}", "/data/format/parse")]
    [InlineData("{\"data\":{\"format\":{\"parse\":{\"a\":\"integer\"}}}}", "/data/format/parse/a")]
    [InlineData("{\"data\":{\"format\":{\"parse\":{\"a\":5}}}}", "/data/format/parse/a")]
    [InlineData("{\"data\":{\"format\":{\"parse\":{\"a\":null}}}}", "/data/format/parse/a")]
    [InlineData("{\"data\":{\"format\":{\"property\":3}}}", "/data/format/property")]
    [InlineData("{\"encoding\":[]}", "/encoding")]
    [InlineData("{\"encoding\":{\"x\":5}}", "/encoding/x")]
    [InlineData("{\"encoding\":{\"x\":[{}]}}", "/encoding/x")]
    [InlineData("{\"encoding\":{\"x\":{\"type\":\"numeric\"}}}", "/encoding/x/type")]
    [InlineData("{\"encoding\":{\"x\":{\"field\":3}}}", "/encoding/x/field")]
    [InlineData("{\"encoding\":{\"x\":{\"scale\":{\"type\":\"cubic\"}}}}", "/encoding/x/scale/type")]
    [InlineData("{\"encoding\":{\"y\":{\"axis\":{\"orient\":\"middle\"}}}}", "/encoding/y/axis/orient")]
    [InlineData("{\"encoding\":{\"y\":{\"axis\":{\"grid\":\"yes\"}}}}", "/encoding/y/axis/grid")]
    [InlineData("{\"encoding\":{\"color\":{\"legend\":{\"type\":\"bubble\"}}}}", "/encoding/color/legend/type")]
    [InlineData("{\"encoding\":{\"detail\":[1]}}", "/encoding/detail/0")]
    [InlineData("{\"params\":{}}", "/params")]
    [InlineData("{\"params\":[1]}", "/params/0")]
    [InlineData("{\"params\":[{\"value\":1}]}", "/params/0")]
    [InlineData("{\"params\":[{\"name\":3}]}", "/params/0/name")]
    [InlineData("{\"transform\":{}}", "/transform")]
    [InlineData("{\"transform\":[3]}", "/transform/0")]
    [InlineData("{\"transform\":[{\"as\":\"x\"}]}", "/transform/0")]
    [InlineData("{\"transform\":[{\"calculate\":3,\"as\":\"x\"}]}", "/transform/0/calculate")]
    public void SpecParser_Parse_WrongTypeReportsPointer(string json, string pointer)
    {
        // Arrange / Act
        var ex = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse(json, "t.json"));

        // Assert: the pointer names the offending value, and the location has a text position and the file
        Assert.Equal(pointer, ex.Location.Pointer);
        Assert.Equal("t.json", ex.Location.File);
        Assert.True(ex.Location.Line >= 1);
        Assert.True(ex.Location.Column >= 1);
        Assert.StartsWith("t.json:", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the Phase 4 data forms are accepted: delimiters, parse directives (including custom date formats,
    ///     which the feature catalog reports), sequences with a negative or zero step, and the kinds of top-level datasets.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_Phase4DataFormsAreAccepted()
    {
        // Arrange: each form on its own line so that a failure points at it
        const string json = """
            { "datasets": { "rows": [ { "a": 1 } ], "text": "a,b\n1,2", "geo": { "type": "Topology" } },
              "layer": [
              { "data": { "url": "a.txt", "format": { "type": "dsv", "delimiter": "|" } } },
              { "data": { "url": "a.txt", "format": { "type": "dsv", "delimiter": "\t" } } },
              { "data": { "url": "a.csv", "format": { "parse": "auto" } } },
              { "data": { "url": "a.csv", "format": { "parse": null } } },
              { "data": { "url": "a.csv", "format": { "parse": { "a": "number", "b": "boolean", "c": "date", "d": "string", "e": "date:'%d/%m/%Y'", "f": "utc:'%Y'" } } } },
              { "data": { "url": "a.json", "format": { "type": "json", "property": "x.y" } } },
              { "data": { "sequence": { "start": 5, "stop": 0, "step": -1, "as": "n" } } },
              { "data": { "sequence": { "start": 0, "stop": 5, "step": 0 } } },
              { "data": { "name": "rows" } }
            ] }
            """;

        // Act
        var tree = SpecParser.Parse(json);

        // Assert: accepted without a parse warning (the layers themselves are only reported by the catalog)
        Assert.Empty(tree.Warnings);
        Assert.Equal(9, tree.Root["layer"]!.Items.Count);
    }

    /// <summary>
    ///     Validates that the first problem in the text is the one reported, also across nested views.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_FirstWrongValueInDocumentOrderIsReported()
    {
        // Arrange: the nested view comes before the bad top-level mark in the text
        const string json = "{\"layer\":[{\"mark\":3}],\"mark\":4}";

        // Act
        var ex = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse(json));

        // Assert
        Assert.Equal("/layer/0/mark", ex.Location.Pointer);
    }

    /// <summary>
    ///     Validates that valid forms of the checked properties are accepted, including expression references.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_ValidFormsOfCheckedPropertiesAreAccepted()
    {
        // Arrange: original specification touching every checked property with a valid but unusual form
        const string json = """
            {
              "$schema": "https://vega.github.io/schema/vega-lite/v6.json",
              "title": { "text": ["a", "b"], "subtitle": { "expr": "'s'" }, "anchor": null, "fontSize": { "expr": "10" }, "color": null },
              "description": "d", "name": "n", "usermeta": { "k": 1 },
              "width": "container", "height": { "step": 12 },
              "padding": { "left": 1, "right": 2, "top": 3, "bottom": 4 },
              "autosize": { "type": "fit", "contains": "padding" },
              "background": { "expr": "'red'" },
              "config": { "view": { "stroke": null } },
              "data": { "name": "t", "format": { "parse": null, "type": "json" } },
              "datasets": { "t": [ { "a": 1 } ], "csv": "a,b\n1,2" },
              "mark": { "type": "bar", "style": ["x", "y"], "discreteBandSize": { "band": 0.5 }, "color": { "gradient": "linear" } },
              "encoding": {
                "x": { "field": "a", "type": "ordinal", "sort": ["b", "a"], "scale": { "type": "band", "padding": { "expr": "0.1" } }, "axis": { "tickCount": "year" } },
                "y": { "aggregate": "count", "type": "quantitative", "axis": null, "scale": null, "bin": { "maxbins": 5 } },
                "tooltip": [ { "field": "a" }, { "field": "b" } ],
                "detail": { "field": "a" }
              },
              "params": [ { "name": "p", "value": 1 }, { "name": "q", "select": "interval" } ],
              "transform": [ { "filter": "datum.a > 1" }, { "calculate": "1", "as": "one" }, { "timeUnit": "year", "field": "d", "as": "y" } ],
              "resolve": { "scale": { "x": "shared" } }, "spacing": 5, "columns": 2, "bounds": "flush", "center": true, "align": "all"
            }
            """;

        // Act
        var tree = SpecParser.Parse(json);

        // Assert: accepted, with no warnings (every key above is a Vega-Lite property)
        Assert.Equal("/title/subtitle", tree.Root["title"]!["subtitle"]!.Pointer);
        Assert.Empty(tree.Warnings);
    }

    /// <summary>
    ///     Validates the duplicate key policy: the last value wins, the property keeps its first position, and one
    ///     warning is reported at the later key.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_DuplicateKeyLastWinsAndWarns()
    {
        // Arrange: the key "mark" is written twice (on lines 2 and 4); the nested object repeats a key as well
        const string json = "{\n  \"mark\": \"bar\",\n  \"name\": \"n\",\n  \"mark\": \"rule\",\n  \"config\": { \"a\": 1, \"a\": 2 }\n}";

        // Act
        var tree = SpecParser.Parse(json, "dup.json");

        // Assert: the last value wins and the property keeps the position of the first occurrence
        Assert.Equal("rule", tree.Root["mark"]!.StringValue);
        Assert.Equal(["mark", "name", "config"], tree.Root.Properties.Select(p => p.Name));
        Assert.Equal(2, tree.Root["config"]!["a"]!.NumberValue);

        // Assert: one warning per duplicate, at the later key, with the stable identifier
        Assert.Equal(2, tree.Warnings.Count);
        var warning = tree.Warnings[0];
        Assert.Equal("vl-duplicate-key", warning.Id);
        Assert.Equal(FeatureReportKind.Warning, warning.Kind);
        Assert.Equal("dup.json", warning.Location.File);
        Assert.Equal("/mark", warning.Location.Pointer);
        Assert.Equal((4, 3), (warning.Location.Line, warning.Location.Column));
        Assert.Equal("/config/a", tree.Warnings[1].Location.Pointer);

        // Assert: a large object (which uses a name index) follows the same policy
        var many = "{" + string.Join(",", Enumerable.Range(0, 20).Select(i => $"\"k{i}\":{i}")) + ",\"k3\":99,\"k19\":98}";
        var large = SpecParser.Parse(many);
        Assert.Equal(99, large.Root["k3"]!.NumberValue);
        Assert.Equal(98, large.Root["k19"]!.NumberValue);
        Assert.Equal(20, large.Root.Properties.Count);
        Assert.Equal(["/k3", "/k19"], large.Warnings.Where(w => w.Id == "vl-duplicate-key").Select(w => w.Location.Pointer));
    }

    /// <summary>
    ///     Validates that excessive nesting, an oversized source, an overlong string and a number out of range fail
    ///     closed with the documented exception and location.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_DepthAndSourceLimitsFailClosed()
    {
        // Arrange
        var shallow = new VegaLiteLimits { MaxJsonDepth = 3 };
        var tiny = new VegaLiteLimits { MaxSourceLength = 20 };
        var short16 = new VegaLiteLimits { MaxStringLength = 16 };

        // Act / Assert: nesting up to the limit is accepted, one more level is a located specification error
        _ = SpecParser.Parse("{\"a\":[[1]]}", null, shallow);
        var deep = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse("{\"a\":\n[[[1]]]}", "deep.json", shallow));
        Assert.Contains("deeper than the limit of 3", deep.Message, StringComparison.Ordinal);
        Assert.Equal("deep.json", deep.Location.File);
        Assert.True(deep.Location.Line >= 1);

        // Act / Assert: hostile nesting under the default limit fails closed without overflowing the stack
        var hostile = string.Concat(Enumerable.Repeat("{\"layer\":[", 5000)) + "{}" + string.Concat(Enumerable.Repeat("]}", 5000));
        Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse(hostile));
        Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse(hostile, null, new VegaLiteLimits { MaxJsonDepth = 256 }));

        // Act / Assert: source over the limit is an InvalidDataException (no location is meaningful), exactly at the limit is fine
        var padded = "{\"title\":\"" + new string('x', 30) + "\"}";
        Assert.Throws<InvalidDataException>(() => SpecParser.Parse(padded, null, tiny));
        Assert.Throws<InvalidDataException>(() => SpecParser.Parse(Encoding.UTF8.GetBytes(padded), null, tiny));
        Assert.Throws<InvalidDataException>(() => SpecParser.Parse(new string(' ', 4_194_305)));
        _ = SpecParser.Parse("{}", null, new VegaLiteLimits { MaxSourceLength = 2 });
        _ = SpecParser.Parse("\uFEFF{}", null, new VegaLiteLimits { MaxSourceLength = 2 });

        // Act / Assert: an overlong string value or key is a located specification error
        var longValue = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse("{\"title\":\"" + new string('y', 17) + "\"}", null, short16));
        Assert.Equal("/title", longValue.Location.Pointer);
        Assert.Contains("MaxStringLength", longValue.Message, StringComparison.Ordinal);
        var longKey = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse("{\"config\":{\"" + new string('k', 17) + "\":1}}", null, short16));
        Assert.StartsWith("/config/", longKey.Location.Pointer, StringComparison.Ordinal);
        var inArray = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse("{\"config\":{\"a\":[1,\"" + new string('z', 17) + "\"]}}", null, short16));
        Assert.Equal("/config/a/1", inArray.Location.Pointer);

        // Act / Assert: a number that is not finite as a double is rejected, with a pointer
        var huge = Assert.Throws<VegaLiteSpecException>(() => SpecParser.Parse("{\"config\":{\"n\":1e999}}"));
        Assert.Equal("/config/n", huge.Location.Pointer);
    }

    /// <summary>
    ///     Validates the conservative unknown-property policy: unknown properties warn once at their key, valid
    ///     Vega-Lite keys never warn.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_UnknownPropertiesWarnOnceAndValidKeysNeverWarn()
    {
        // Arrange: unknown keys at the view, mark, encoding and channel-definition levels; valid but unusual keys elsewhere
        const string json = """
            {
              "mark": { "type": "point", "bogusMark": 1, "tooltip": true, "ariaRole": "x", "timeUnitBandPosition": 0.5 },
              "bogusView": 1,
              "selection": {},
              "usermeta": { "anything": "goes" },
              "config": { "whatever": 1 },
              "encoding": {
                "x": { "field": "a", "type": "nominal", "bogusChannelDef": 1, "band": 0.5, "bandPosition": 0.5, "rescale": true },
                "hue": { "field": "b" },
                "row": { "field": "c" }
              },
              "layer": [ { "bogusNested": 1 } ]
            }
            """;

        // Act
        var tree = SpecParser.Parse(json, "u.json");

        // Assert: one warning per unknown property, at the key (not the value)
        var unknown = tree.Warnings.Where(w => w.Id == "vl-unknown-property").ToList();
        Assert.Equal(
            ["/mark/bogusMark", "/bogusView", "/encoding/x/bogusChannelDef", "/encoding/hue", "/layer/0/bogusNested"],
            unknown.OrderBy(w => w.Location.Line).Select(w => w.Location.Pointer));
        Assert.All(unknown, w => Assert.Equal(FeatureReportKind.Warning, w.Kind));
        Assert.Equal(5, unknown.Count);
        var bogusView = unknown.Single(w => w.Location.Pointer == "/bogusView");
        Assert.Equal((3, 3), (bogusView.Location.Line, bogusView.Location.Column));
    }

    /// <summary>
    ///     Validates that every property name of the Vega-Lite version 6 sets used for the unknown-property check is
    ///     accepted without a warning (a regression guard for the conservative policy).
    /// </summary>
    /// <param name="json">A specification that uses one top-level property with a valid value.</param>
    [Theory]
    [InlineData("{\"align\":\"all\"}")]
    [InlineData("{\"bounds\":\"flush\"}")]
    [InlineData("{\"center\":true}")]
    [InlineData("{\"columns\":2}")]
    [InlineData("{\"datasets\":{}}")]
    [InlineData("{\"projection\":{}}")]
    [InlineData("{\"resolve\":{}}")]
    [InlineData("{\"spacing\":5}")]
    [InlineData("{\"usermeta\":{}}")]
    [InlineData("{\"view\":{}}")]
    [InlineData("{\"description\":\"d\"}")]
    public void SpecParser_Parse_ValidTopLevelNamesDoNotWarn(string json)
    {
        // Arrange / Act
        var tree = SpecParser.Parse(json);

        // Assert
        Assert.Empty(tree.Warnings);
    }

    /// <summary>
    ///     Validates that the schema major version is read from the URL with the documented forms.
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
    public void SpecParser_ParseSchemaMajorVersion_ParsesTheMajorVersion(string url, int expected)
    {
        // Arrange / Act
        var version = SpecParser.ParseSchemaMajorVersion(url);

        // Assert
        Assert.Equal(expected < 0 ? null : expected, version);
    }

    /// <summary>
    ///     Validates that the views of a composition are enumerated, parents before children, with explicit nesting.
    /// </summary>
    [Fact]
    public void SpecTree_EnumerateViews_ListsNestedViewsParentsFirst()
    {
        // Arrange
        const string json = """
            { "layer": [ { "layer": [ { "mark": "rule" } ] }, { "mark": "bar" } ],
              "vconcat": [ { "mark": "line" } ], "spec": { "mark": "area" }, "encoding": { "x": { "layer": [ {} ] } } }
            """;

        // Act
        var views = SpecParser.Parse(json).EnumerateViews();

        // Assert: the encoding's "layer" is not a view composition
        Assert.Equal(
            ["", "/layer/0", "/layer/0/layer/0", "/layer/1", "/vconcat/0", "/spec"],
            views.Select(v => v.Pointer));
    }

    /// <summary>
    ///     Validates the metadata read from the tree: schema, title, marks, data references and parameter names, all in
    ///     document order with their locations.
    /// </summary>
    [Fact]
    public void SpecMetadata_Read_DescribesSchemaTitleMarksDataAndParameters()
    {
        // Arrange
        const string json = """
            {
              "$schema": "https://vega.github.io/schema/vega-lite/v5.json",
              "title": { "text": ["A", "B"], "subtitle": "S" },
              "params": [ { "name": "p" }, { "name": "q" } ],
              "data": { "values": [ {"a":1}, {"a":2}, [1,2,3] ] },
              "layer": [
                { "mark": "bar", "data": { "url": "points.csv", "format": { "type": "csv" } } },
                { "mark": { "type": "point", "filled": true }, "data": { "name": "table" }, "params": [ { "name": "p" } ] },
                { "data": { "sequence": { "start": 0, "stop": 10, "step": 2 } } },
                { "data": { "values": [] } },
                { "data": { "values": "a,b\n1,2" } },
                { "data": null },
                { "data": { "format": {} } },
                {},
                { "layer": [ { "mark": "rule" } ] }
              ],
              "mark": "text",
              "vconcat": [ { "mark": "line" } ],
              "spec": { "mark": "area" }
            }
            """;

        // Act
        var metadata = SpecMetadata.Read(SpecParser.Parse(json, "m.json"));

        // Assert: schema and title
        Assert.Equal("https://vega.github.io/schema/vega-lite/v5.json", metadata.SchemaUrl);
        Assert.Equal(5, metadata.SchemaMajorVersion);
        Assert.Equal("A\nB", metadata.Title);

        // Assert: marks in the order they are written, with pointers and positions
        Assert.Equal(["bar", "point", "rule", "text", "line", "area"], metadata.Marks.Select(m => m.Type));
        Assert.Equal(
            ["/layer/0/mark", "/layer/1/mark", "/layer/8/layer/0/mark", "/mark", "/vconcat/0/mark", "/spec/mark"],
            metadata.Marks.Select(m => m.Location.Pointer));
        Assert.Equal(("m.json", 7), (metadata.Marks[0].Location.File, metadata.Marks[0].Location.Line));

        // Assert: data references by kind, row counts, urls and locations; views without a source add none
        Assert.Equal(
            [VegaLiteDataKind.Inline, VegaLiteDataKind.Url, VegaLiteDataKind.Name, VegaLiteDataKind.Sequence, VegaLiteDataKind.Inline, VegaLiteDataKind.Inline],
            metadata.DataReferences.Select(r => r.Kind));
        Assert.Equal([3L, null, null, 5L, 0L, null], metadata.DataReferences.Select(r => r.RowCount));
        Assert.Equal("points.csv", metadata.DataReferences[1].Url);
        Assert.Equal(
            ["/data", "/layer/0/data", "/layer/1/data", "/layer/2/data", "/layer/3/data", "/layer/4/data"],
            metadata.DataReferences.Select(r => r.Location.Pointer));

        // Assert: distinct parameter names in order
        Assert.Equal(["p", "q"], metadata.ParameterNames);
    }

    /// <summary>
    ///     Validates the title text for each form.
    /// </summary>
    /// <param name="json">The specification text.</param>
    /// <param name="expected">The expected plain text.</param>
    [Theory]
    [InlineData("{\"title\":\"Sales\"}", "Sales")]
    [InlineData("{\"title\":[\"A\",\"B\"]}", "A\nB")]
    [InlineData("{\"title\":{\"text\":\"T\",\"subtitle\":\"S\"}}", "T")]
    [InlineData("{\"title\":{\"text\":[\"A\",\"B\"]}}", "A\nB")]
    [InlineData("{\"title\":{\"subtitle\":\"S\"}}", null)]
    [InlineData("{\"title\":{\"text\":{\"expr\":\"x\"}}}", null)]
    [InlineData("{\"title\":null}", null)]
    [InlineData("{}", null)]
    public void SpecMetadata_Read_TitleIsPlainText(string json, string? expected)
    {
        // Arrange / Act
        var metadata = SpecMetadata.Read(SpecParser.Parse(json));

        // Assert
        Assert.Equal(expected, metadata.Title);
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
    [InlineData("{\"start\":0,\"stop\":1e300}", -1)]
    public void SpecMetadata_Read_SequenceComputesLength(string sequence, long expected)
    {
        // Arrange
        var json = "{\"data\":{\"sequence\":" + sequence + "}}";

        // Act
        var reference = Assert.Single(SpecMetadata.Read(SpecParser.Parse(json)).DataReferences);

        // Assert
        Assert.Equal(VegaLiteDataKind.Sequence, reference.Kind);
        Assert.Equal(expected < 0 ? null : expected, reference.RowCount);
    }

    /// <summary>
    ///     Validates that inline values win over the other source keys of a data object.
    /// </summary>
    [Fact]
    public void SpecMetadata_Read_InlineValuesTakePrecedenceOverName()
    {
        // Arrange: a named inline data set
        const string json = "{\"data\":{\"name\":\"t\",\"values\":[{\"a\":1}]}}";

        // Act
        var reference = Assert.Single(SpecMetadata.Read(SpecParser.Parse(json)).DataReferences);

        // Assert
        Assert.Equal(VegaLiteDataKind.Inline, reference.Kind);
        Assert.Equal(1, reference.RowCount);
    }

    /// <summary>
    ///     Validates that a node describes itself with its kind, pointer and position.
    /// </summary>
    [Fact]
    public void SpecNode_ToString_DescribesKindPointerAndPosition()
    {
        // Arrange
        var tree = SpecParser.Parse("{\"a\":[1]}");

        // Act / Assert
        Assert.Equal("Object (root) @1:1", tree.Root.ToString());
        Assert.Equal("Number /a/0 @1:7", tree.Root["a"]!.Items[0].ToString());
        Assert.Equal("/a/0", tree.Root["a"]!.ChildPointer("0"));
        Assert.Equal("/a/x~1y", tree.Root["a"]!.ChildPointer("x/y"));
        Assert.Equal(0, tree.Root["a"]!.Items[0].Index);
        Assert.Equal(-1, tree.Root["a"]!.Index);
    }
}
