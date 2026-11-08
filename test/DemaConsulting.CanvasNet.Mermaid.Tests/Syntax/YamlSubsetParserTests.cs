using System.Globalization;
using System.Text;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Syntax;

/// <summary>
///     Unit tests for <see cref="YamlSubsetParser"/>: the results of js-yaml's <c>load</c> with the <c>JSON_SCHEMA</c>
///     (the expected values were produced with the js-yaml version Mermaid bundles), the refused constructs and the
///     limits.
/// </summary>
public class YamlSubsetParserTests
{
    /// <summary>The JSON options that write strings as JavaScript's <c>JSON.stringify</c> does (no HTML escaping).</summary>
    private static readonly System.Text.Json.JsonSerializerOptions Relaxed = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    ///     Parses a text with locations in the text itself.
    /// </summary>
    /// <param name="text">The YAML text.</param>
    /// <param name="maxDepth">The collection depth limit.</param>
    /// <returns>The value.</returns>
    private static YamlNode? Parse(string text, int maxDepth = 32)
    {
        var lines = new SourceLines(text, null);
        return YamlSubsetParser.Parse(text, lines.Locate, maxDepth);
    }

    /// <summary>
    ///     Converts a value to compact JSON for comparison with js-yaml's results.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <returns>The JSON text.</returns>
    private static string Json(YamlNode? node)
    {
        var builder = new StringBuilder();
        Write(builder, node);
        return builder.ToString();
    }

    /// <summary>
    ///     Writes a value as compact JSON (JavaScript's <c>JSON.stringify</c>: infinity and NaN are null).
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="node">The value.</param>
    private static void Write(StringBuilder builder, YamlNode? node)
    {
        switch (node)
        {
            case null:
            case YamlScalar { Value: null }:
                builder.Append("null");
                break;
            case YamlScalar { Value: bool b }:
                builder.Append(b ? "true" : "false");
                break;
            case YamlScalar { Value: double d }:
                builder.Append(double.IsFinite(d) ? YamlSubsetParser.FormatJsNumber(d) : "null");
                break;
            case YamlScalar { Value: string s }:
                builder.Append(System.Text.Json.JsonSerializer.Serialize(s, Relaxed));
                break;
            case YamlSequence sequence:
                builder.Append('[');
                for (var i = 0; i < sequence.Items.Count; i++)
                {
                    builder.Append(i > 0 ? "," : string.Empty);
                    Write(builder, sequence.Items[i]);
                }

                builder.Append(']');
                break;
            case YamlMapping mapping:
                builder.Append('{');
                for (var i = 0; i < mapping.Entries.Count; i++)
                {
                    builder.Append(i > 0 ? "," : string.Empty).Append(System.Text.Json.JsonSerializer.Serialize(mapping.Entries[i].Key, Relaxed)).Append(':');
                    Write(builder, mapping.Entries[i].Value);
                }

                builder.Append('}');
                break;
        }
    }

    /// <summary>
    ///     Validates documents against the results of js-yaml.
    /// </summary>
    /// <param name="yaml">The YAML text.</param>
    /// <param name="expected">The JSON of js-yaml's result.</param>
    [Theory]
    [InlineData("a: 1\nb:\n  c: [x, 'y', \"z\\tq\"]\n  d:\n  - 1\n  - two\ne: {f: g, h}\n", "{\"a\":1,\"b\":{\"c\":[\"x\",\"y\",\"z\\tq\"],\"d\":[1,\"two\"]},\"e\":{\"f\":\"g\",\"h\":null}}")]
    [InlineData("key:\n- a\n- b: c\n  d: e\n", "{\"key\":[\"a\",{\"b\":\"c\",\"d\":\"e\"}]}")]
    [InlineData("lit: |\n  line1\n   more\n\n  line3\nfold: >-\n  a\n  b\n\n  c\nkeep: |+\n  x\n\nend: 1\n", "{\"lit\":\"line1\\n more\\n\\nline3\\n\",\"fold\":\"a b\\nc\",\"keep\":\"x\\n\\n\",\"end\":1}")]
    [InlineData("plain: hello\n  world\n\n  again # comment\nq: 'it''s\n  folded'\n", "{\"plain\":\"hello world\\nagain\",\"q\":\"it's folded\"}")]
    [InlineData("[a: 1, b]\n", "[{\"a\":1},\"b\"]")]
    [InlineData("{\"json\":1, 'x' : [1,2,], y: }\n", "{\"json\":1,\"x\":[1,2],\"y\":null}")]
    [InlineData("dq: \"a\\x41\\u00e9\\\n   b\"\n", "{\"dq\":\"aA\u00e9b\"}")]
    [InlineData("-\n- x\n-   \n", "[null,\"x\",null]")]
    [InlineData("hello\nworld\n", "\"hello world\"")]
    [InlineData("# only comment\n", "null")]
    [InlineData("", "null")]
    [InlineData("a: 1\n...\n", "{\"a\":1}")]
    [InlineData("[a b]\n", "[\"a b\"]")]
    [InlineData("a:1\n", "\"a:1\"")]
    [InlineData("---\na: 1\n", "{\"a\":1}")]
    public void YamlSubsetParser_Parse_Documents_MatchJsYaml(string yaml, string expected)
    {
        // Arrange / Act
        var result = Json(Parse(yaml));

        // Assert
        Assert.Equal(expected, result);
    }

    /// <summary>
    ///     Validates the implicit scalar types of the <c>JSON_SCHEMA</c> against js-yaml.
    /// </summary>
    [Fact]
    public void YamlSubsetParser_Parse_ImplicitTypes_MatchJsonSchema()
    {
        // Arrange
        const string Yaml = "n1: ~\nn2: null\nn3: NULL\nn4:\nb1: True\nb2: FALSE\ni1: 0x1F\ni2: -0o17\ni3: 0b101\ni4: 012\ni5: 1_000\n" +
                            "f1: 1.5e3\nf2: .5\nf3: -.inf\nf4: .NaN\nf5: 1.\ns1: 1e400\ns2: -.5\ns3: 0x\n";

        // Act
        var mapping = Assert.IsType<YamlMapping>(Parse(Yaml));
        object? Value(string key) => ((YamlScalar)mapping.Get(key)!).Value;

        // Assert
        Assert.All(["n1", "n2", "n3", "n4"], key => Assert.Null(Value(key)));
        Assert.Equal((true, false), ((bool)Value("b1")!, (bool)Value("b2")!));
        Assert.Equal([31.0, -15.0, 5.0, 12.0], new[] { "i1", "i2", "i3", "i4" }.Select(k => (double)Value(k)!));
        Assert.Equal("1_000", Value("i5"));
        Assert.Equal((1500.0, 0.5, 1.0), ((double)Value("f1")!, (double)Value("f2")!, (double)Value("f5")!));
        Assert.Equal(double.NegativeInfinity, Value("f3"));
        Assert.True(double.IsNaN((double)Value("f4")!));
        Assert.Equal(["1e400", "-.5", "0x"], new[] { "s1", "s2", "s3" }.Select(k => Value(k)));
    }

    /// <summary>
    ///     Validates js-yaml's errors and their locations.
    /// </summary>
    /// <param name="yaml">The invalid YAML text.</param>
    /// <param name="message">js-yaml's message.</param>
    /// <param name="line">The expected line.</param>
    /// <param name="column">The expected column.</param>
    [Theory]
    [InlineData("a: 1\na: 2\n", "duplicated mapping key", 2, 1)]
    [InlineData("a:\n\t- x\n", "tab characters must not be used in indentation", 2, 1)]
    [InlineData("a: 1\n  b: 2\n", "bad indentation of a mapping entry", 2, 4)]
    [InlineData("a: 'x\n", "unexpected end of the stream within a single quoted scalar", 2, 1)]
    [InlineData("a: b: c\n", "bad indentation of a mapping entry", 1, 5)]
    [InlineData("x:\n  - a\n - b\n", "bad indentation of a mapping entry", 3, 2)]
    [InlineData("{a: 1\n", "unexpected end of the stream within a flow collection", 2, 1)]
    [InlineData("a: \"\\q\"\n", "unknown escape sequence", 1, 6)]
    [InlineData("a: 1\n---\nb: 2\n", "expected a single document in the stream, but found more", 2, 1)]
    public void YamlSubsetParser_Parse_InvalidYaml_ThrowsLocatedJsYamlError(string yaml, string message, int line, int column)
    {
        // Arrange / Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => Parse(yaml));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.YamlInvalid, ex.Code);
        Assert.Contains(message, ex.Message, StringComparison.Ordinal);
        Assert.Equal(new MermaidLocation(null, line, column), ex.Location);
    }

    /// <summary>
    ///     Validates that anchors, aliases, tags, explicit keys and directives are refused.
    /// </summary>
    /// <param name="yaml">The YAML text.</param>
    [Theory]
    [InlineData("&x a: 1\n")]
    [InlineData("a: &x 1\n")]
    [InlineData("a: *x\n")]
    [InlineData("a: !!str 1\n")]
    [InlineData("? a\n: b\n")]
    [InlineData("%YAML 1.2\n---\na: 1\n")]
    [InlineData("[*a]\n")]
    public void YamlSubsetParser_Parse_RefusedConstruct_ThrowsUnsupported(string yaml)
    {
        // Arrange / Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => Parse(yaml));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.YamlUnsupported, ex.Code);
        Assert.NotNull(ex.Hint);
    }

    /// <summary>
    ///     Validates the depth limit for block and flow collections.
    /// </summary>
    [Fact]
    public void YamlSubsetParser_Parse_TooDeep_ThrowsLimitExceeded()
    {
        // Arrange
        var flow = new string('[', 4) + new string(']', 4);
        var block = "a:\n  b:\n    c:\n      d: 1\n";

        // Act
        var flowError = Assert.Throws<MermaidSyntaxException>(() => Parse(flow, maxDepth: 3));
        var blockError = Assert.Throws<MermaidSyntaxException>(() => Parse(block, maxDepth: 3));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, flowError.Code);
        Assert.Contains("MermaidLimits.MaxYamlDepth", blockError.Message, StringComparison.Ordinal);
        Assert.Equal(new MermaidLocation(null, 1, 4), flowError.Location);
        Assert.NotNull(Parse(flow, maxDepth: 4));
        Assert.NotNull(Parse(block, maxDepth: 4));
    }

    /// <summary>
    ///     Validates that a deeply nested hostile text is refused without overflowing the stack.
    /// </summary>
    [Fact]
    public void YamlSubsetParser_Parse_HostileNesting_IsBounded()
    {
        // Arrange
        var hostile = new string('[', 100_000);

        // Act / Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, Assert.Throws<MermaidSyntaxException>(() => Parse(hostile)).Code);
    }

    /// <summary>
    ///     Validates the locations of values and keys.
    /// </summary>
    [Fact]
    public void YamlSubsetParser_Parse_Nodes_CarryLocations()
    {
        // Arrange / Act
        var mapping = Assert.IsType<YamlMapping>(Parse("a: 1\nlist:\n  - x\n"));

        // Assert
        Assert.Equal(new MermaidLocation(null, 1, 1), mapping.Location);
        Assert.Equal(new MermaidLocation(null, 2, 1), mapping.Entries[1].KeyLocation);
        var list = Assert.IsType<YamlSequence>(mapping.Get("list"));
        Assert.Equal(new MermaidLocation(null, 3, 5), list.Items[0].Location);
        Assert.Null(mapping.Get("missing"));
    }

    /// <summary>
    ///     Validates JavaScript's number to text conversion against <c>String(n)</c>.
    /// </summary>
    /// <param name="value">The number, as invariant text.</param>
    /// <param name="expected">JavaScript's text.</param>
    [Theory]
    [InlineData("1.5", "1.5")]
    [InlineData("100", "100")]
    [InlineData("1e21", "1e+21")]
    [InlineData("1e-7", "1e-7")]
    [InlineData("0.000001", "0.000001")]
    [InlineData("123456789012345680000", "123456789012345680000")]
    [InlineData("-0", "0")]
    [InlineData("5e-324", "5e-324")]
    [InlineData("1.7976931348623157e308", "1.7976931348623157e+308")]
    [InlineData("-1234.5678", "-1234.5678")]
    [InlineData("1e20", "100000000000000000000")]
    [InlineData("123e-20", "1.23e-18")]
    [InlineData("NaN", "NaN")]
    [InlineData("-Infinity", "-Infinity")]
    public void YamlSubsetParser_FormatJsNumber_MatchesJavaScript(string value, string expected)
    {
        // Arrange
        var number = double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);

        // Act / Assert
        Assert.Equal(expected, YamlSubsetParser.FormatJsNumber(number));
    }

    /// <summary>
    ///     Validates JavaScript truthiness and string conversion of values.
    /// </summary>
    [Fact]
    public void YamlNode_TruthinessAndString_FollowJavaScript()
    {
        // Arrange
        var sequence = Assert.IsType<YamlSequence>(Parse("[1, null, a, true]"));
        var mapping = Assert.IsType<YamlMapping>(Parse("{}"));

        // Act / Assert
        Assert.Equal("1,,a,true", sequence.ToJsString());
        Assert.Equal("[object Object]", mapping.ToJsString());
        Assert.True(mapping.IsTruthy);
        Assert.False(new YamlScalar(0.0, default).IsTruthy);
        Assert.False(new YamlScalar(double.NaN, default).IsTruthy);
        Assert.False(new YamlScalar(string.Empty, default).IsTruthy);
        Assert.True(new YamlScalar("0", default).IsTruthy);
        Assert.Equal("null", new YamlScalar(null, default).ToJsString());
    }
}
