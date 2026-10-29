using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.SpecModel;

/// <summary>
///     Tests of how <see cref="VegaLiteDocument"/> uses the specification model: the metadata it reports, the findings it
///     delivers when measured and rendered, and the configuration it resolves.
/// </summary>
public class VegaLiteDocumentSpecModelTests
{
    /// <summary>Gets the cancellation token of the running test, so a canceled test run stops rendering promptly.</summary>
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Validates that parse warnings and catalog reports reach the handlers together, in document order, once per
    ///     call, and that measuring delivers the same events as rendering.
    /// </summary>
    [Fact]
    public void VegaLiteDocument_Render_DeliversParseWarningsAndCatalogReportsInDocumentOrder()
    {
        // Arrange: a duplicate key, an unknown property, an unsupported mark and a deferred transform, in text order
        const string json = """
            {
              "name": "a",
              "name": "b",
              "bogus": 1,
              "mark": "arc",
              "transform": [ { "filter": "datum.x > 1" } ]
            }
            """;
        var document = VegaLiteDocument.Parse(json, new VegaLiteOpenOptions { SourceName = "w.vl.json" });

        // Act
        var renderEvents = Collect(document, render: true);
        var measureEvents = Collect(document, render: false);

        // Assert: text order, with the position of the offending key or value
        Assert.Equal(
            [
                "W vl-duplicate-key /name 3:3",
                "W vl-unknown-property /bogus 4:3",
                "U vl-mark-arc /mark 5:11",
                "U vl-transform-filter /transform/0 6:18",
                "U vl-expr-filter /transform/0/filter 6:30",
            ],
            renderEvents);
        Assert.Equal(renderEvents, measureEvents);
        Assert.Equal(renderEvents, Collect(document, render: true));
    }

    /// <summary>
    ///     Validates that strict mode throws at the first unsupported feature and that warnings never stop rendering.
    /// </summary>
    [Fact]
    public void VegaLiteDocument_Render_StrictModeIgnoresWarningsAndThrowsAtTheFirstUnsupportedFeature()
    {
        // Arrange: only warnings first, then an unsupported mark
        var document = VegaLiteDocument.Parse("{\"bogus\":1,\"mark\":\"arc\"}");

        // Act / Assert
        var ex = Assert.Throws<VegaLiteUnsupportedFeatureException>(() => document.Render(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true }, Ct));
        Assert.Equal("vl-mark-arc", ex.Feature);

        // Assert: a specification with only warnings renders in strict mode
        using var surface = VegaLiteDocument.Parse("{\"bogus\":1}").Render(new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true }, Ct);
        Assert.Equal(310, surface.Width);
    }

    /// <summary>
    ///     Validates that supported constructs are not reported and that the metadata is read from the tree.
    /// </summary>
    [Fact]
    public void VegaLiteDocument_Open_SupportedChartHasMetadataAndNoReports()
    {
        // Arrange
        const string json = """
            {
              "$schema": "https://vega.github.io/schema/vega-lite/v6.json",
              "title": "Counts",
              "data": { "values": [ { "k": "a", "v": 1 }, { "k": "b", "v": 2 } ] },
              "mark": "bar",
              "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } }
            }
            """;
        var document = VegaLiteDocument.Parse(json);

        // Act
        var events = Collect(document, render: true);

        // Assert
        Assert.Empty(events);
        Assert.Equal(["bar"], document.MarkTypes);
        Assert.Equal("Counts", document.Title);
        Assert.Equal(6, document.SchemaMajorVersion);
        Assert.Equal(2, Assert.Single(document.DataReferences).RowCount);
        Assert.Equal(VegaLiteDataKind.Inline, document.DataReferences[0].Kind);
        Assert.Equal("/data", document.DataReferences[0].Location.Pointer);
        Assert.Empty(document.ParameterNames);
    }

    /// <summary>
    ///     Validates that a wrong value in a checked property fails when the document is opened, with the pointer.
    /// </summary>
    [Fact]
    public void VegaLiteDocument_Parse_WrongTypeFailsWithPointerAndSourceName()
    {
        // Arrange / Act
        var ex = Assert.Throws<VegaLiteSpecException>(() =>
            VegaLiteDocument.Parse("{\"encoding\":{\"x\":{\"type\":\"numeric\"}}}", new VegaLiteOpenOptions { SourceName = "c.vl.json" }));

        // Assert
        Assert.Equal("/encoding/x/type", ex.Location.Pointer);
        Assert.Equal("c.vl.json", ex.Location.File);
        Assert.StartsWith("c.vl.json:1:", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that rendering resolves the caller's configuration and accepts the specification's.
    /// </summary>
    [Fact]
    public void VegaLiteDocument_Render_ResolvesConfigurationFromSpecificationAndOptions()
    {
        // Arrange
        var document = VegaLiteDocument.Parse("{\"config\":{\"view\":{\"continuousWidth\":123}},\"mark\":\"bar\"}");
        var options = new VegaLiteRenderOptions { Config = "{\"background\":\"#123456\",\"view\":{\"continuousHeight\":77}}" };

        // Act
        using var surface = document.Render(options, Ct);
        var resolved = ConfigResolver.Resolve(document.Tree, options.Config);

        // Assert: the render honors the configuration (width from the specification, height and background from the
        // caller) and the same resolution gives caller over specification over defaults
        Assert.Equal((133, 87), (surface.Width, surface.Height));
        Assert.Equal(new Rgba32(0x12, 0x34, 0x56, 255), surface[0, 0]);
        Assert.Equal(123, resolved.GetNumber("view.continuousWidth"));
        Assert.Equal(77, resolved.GetNumber("view.continuousHeight"));
        Assert.Equal("#123456", resolved.GetString("background"));
    }

    /// <summary>
    ///     Validates that a stream or file holding malformed UTF-8 is rejected with a specification exception rather
    ///     than decoded with replacement characters, and that a stream with a byte-order mark still opens.
    /// </summary>
    [Fact]
    public void VegaLiteDocument_Open_MalformedUtf8StreamAndFileAreRejected()
    {
        // Arrange: a stray byte, an overlong NUL and an encoded surrogate inside the title
        byte[][] malformed =
        [
            [.. "{\"title\":\"a"u8, 0xFF, .. "\"}"u8],
            [.. "{\"title\":\"a"u8, 0xC0, 0x80, .. "\"}"u8],
            [.. "{\"title\":\"a"u8, 0xED, 0xA0, 0x80, .. "\"}"u8],
            [0xEF, 0xBB, 0xBF, .. "{\"title\":\"a"u8, 0xE2, 0x82, .. "\"}"u8],
        ];
        var path = Path.Combine(Path.GetTempPath(), "vl-utf8-" + Guid.NewGuid().ToString("N") + ".vl.json");

        try
        {
            foreach (var bytes in malformed)
            {
                // Act / Assert: the stream overload
                using var stream = new MemoryStream(bytes);
                var streamEx = Assert.Throws<VegaLiteSpecException>(() => VegaLiteDocument.Open(stream));
                Assert.Contains("UTF-8", streamEx.Message, StringComparison.Ordinal);

                // Act / Assert: the file overload names the file
                File.WriteAllBytes(path, bytes);
                var fileEx = Assert.Throws<VegaLiteSpecException>(() => VegaLiteDocument.Open(path));
                Assert.Equal(path, fileEx.Location.File);
            }

            // Assert: well-formed multi-byte text with a byte-order mark opens through both overloads
            byte[] good = [0xEF, 0xBB, 0xBF, .. "{\"title\":\"\u00e9\u20ac\U0001F600\"}"u8];
            File.WriteAllBytes(path, good);
            using var goodStream = new MemoryStream(good);
            Assert.Equal("\u00e9\u20ac\U0001F600", VegaLiteDocument.Open(goodStream).Title);
            Assert.Equal("\u00e9\u20ac\U0001F600", VegaLiteDocument.Open(path).Title);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    ///     Validates that a string with an unpaired surrogate is rejected by <see cref="VegaLiteDocument.Parse"/>, and
    ///     that the source limit still applies to a stream of multi-byte characters.
    /// </summary>
    [Fact]
    public void VegaLiteDocument_Parse_UnpairedSurrogateIsRejectedAndStreamSizeLimitIsInCharacters()
    {
        // Act / Assert: a lone surrogate is not text
        Assert.Throws<VegaLiteSpecException>(() => VegaLiteDocument.Parse("{\"title\":\"\uD800\"}"));

        // Arrange: a title of 10 three-byte characters in a document of exactly 22 characters (30 bytes of title)
        var limits = new VegaLiteLimits { MaxSourceLength = 22 };
        var options = new VegaLiteOpenOptions { Limits = limits };
        var fits = "{\"title\":\"" + new string('\u20ac', 10) + "\"}";
        var tooLong = "{\"title\":\"" + new string('\u20ac', 11) + "\"}";

        // Act / Assert: the limit counts characters, not bytes, for a stream just as for a string
        using var okStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(fits));
        Assert.Equal(new string('\u20ac', 10), VegaLiteDocument.Open(okStream, options).Title);
        using var longStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(tooLong));
        Assert.Throws<InvalidDataException>(() => VegaLiteDocument.Open(longStream, options));
    }

    /// <summary>
    ///     Validates that a large inline data set is read in one pass without a per-row cost beyond its size.
    /// </summary>
    [Fact]
    public void SpecParser_Parse_LargeInlineDataIsReadInOnePass()
    {
        // Arrange: 50,000 rows (about 1.5 MB), under the default source limit
        var rows = string.Join(",", Enumerable.Range(0, 50_000).Select(i => "{\"k\":\"c" + i + "\",\"v\":" + i + "}"));
        var json = "{\"data\":{\"values\":[" + rows + "]},\"mark\":\"bar\"}";

        // Act
        var tree = SpecParser.Parse(json);
        var rowsNode = tree.Root["data"]!["values"]!;

        // Assert
        Assert.Equal(50_000, rowsNode.Items.Count);
        Assert.Equal(49_999, rowsNode.Items[49_999]["v"]!.NumberValue);
        Assert.Equal("/data/values/49999/v", rowsNode.Items[49_999]["v"]!.Pointer);
        Assert.Empty(tree.Warnings);
        Assert.Equal(50_000, Assert.Single(SpecMetadata.Read(tree).DataReferences).RowCount);
    }

    /// <summary>
    ///     Collects the events of a measure or render call as short text lines.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="render">Whether to render (otherwise measure).</param>
    /// <returns>One line per event: kind, identifier, pointer, line and column.</returns>
    private static List<string> Collect(VegaLiteDocument document, bool render)
    {
        var events = new List<string>();
        var options = new VegaLiteRenderOptions
        {
            WarningHandler = w => events.Add($"W {w.Message[..w.Message.IndexOf(':', StringComparison.Ordinal)]} {w.Location.Pointer} {w.Location.Line}:{w.Location.Column}"),
            UnsupportedFeatureHandler = u => events.Add($"U {u.Feature} {u.Location.Pointer} {u.Location.Line}:{u.Location.Column}"),
        };
        if (render)
        {
            using var surface = document.Render(options, Ct);
        }
        else
        {
            _ = document.Measure(options, Ct);
        }

        return events;
    }
}
