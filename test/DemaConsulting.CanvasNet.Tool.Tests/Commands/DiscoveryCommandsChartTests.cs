using System.Text.Json;
using DemaConsulting.CanvasNet.Tool.Commands;
using DemaConsulting.CanvasNet.Tool.Imaging;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Unit tests for the DiscoveryCommands class with Vega-Lite charts: the listing of the format and the info
///     description of a chart.
/// </summary>
[Collection("Sequential")]
public class DiscoveryCommandsChartTests
{
    /// <summary>
    ///     A chart with a title, two marks, a parameter, and an inline, a URL, and a named data source.
    /// </summary>
    private const string RichChart =
        """
        {
          "$schema": "https://vega.github.io/schema/vega-lite/v5.json",
          "title": "Info test",
          "width": 100,
          "height": 50,
          "params": [ { "name": "cutoff", "value": 3 } ],
          "data": { "values": [ { "k": "a", "v": 3 }, { "k": "b", "v": 5 } ] },
          "layer": [
            { "mark": "bar", "encoding": { "x": { "field": "k", "type": "nominal" }, "y": { "field": "v", "type": "quantitative" } } },
            { "mark": { "type": "rule" }, "data": { "name": "limits" } }
          ]
        }
        """;

    /// <summary>
    ///     Gets the natural size of a chart, as the info command reports it.
    /// </summary>
    /// <param name="path">The path of the chart.</param>
    /// <returns>The width and height in pixels.</returns>
    internal static (int Width, int Height) NaturalSize(string path)
    {
        var info = SourceLoader.Probe(path);
        return (info.Width, info.Height);
    }

    /// <summary>
    ///     Runs the info command and captures what it writes.
    /// </summary>
    /// <param name="json"><see langword="true"/> to run with the global <c>--json</c> option.</param>
    /// <param name="args">The command arguments.</param>
    /// <returns>The captured standard output.</returns>
    private static string RunInfo(bool json, params string[] args)
    {
        using var context = Context.Create(json ? ["--json"] : []);
        var command = new DiscoveryCommands.Info();
        var (stdOut, _) = ToolHost.Capture(() => command.Run(context, args));
        return stdOut;
    }

    /// <summary>
    ///     Test that the formats listing names the chart format as a vector kind that is not multi-page, with both of
    ///     its extensions, in the text and in the JSON, and that the loader reads a file of that kind.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Formats_IncludesVegaLiteChart()
    {
        // Arrange
        using var directory = new TempDirectory();
        using var json = Context.Create(["--json"]);
        using var text = Context.Create([]);
        var command = new DiscoveryCommands.Formats("1.2.3", "4.5.6");

        // Act
        var (jsonOut, _) = ToolHost.Capture(() => command.Run(json, []));
        var (textOut, _) = ToolHost.Capture(() => command.Run(text, []));

        // Assert: the table row, the JSON row, and the text row
        var chart = Assert.Single(DiscoveryCommands.InputFormats, r => r.Name == "vega-lite");
        Assert.Equal(("chart", true, false), (chart.Kind, chart.Vector, chart.MultiPage));
        Assert.Equal([".vl.json", ".vl"], chart.Extensions);
        using var document = JsonDocument.Parse(jsonOut);
        var row = document.RootElement.GetProperty("inputs").EnumerateArray().Single(i => i.GetProperty("format").GetString() == "vega-lite");
        Assert.Equal("chart", row.GetProperty("kind").GetString());
        Assert.Equal([".vl.json", ".vl"], row.GetProperty("extensions").EnumerateArray().Select(e => e.GetString()));
        Assert.False(row.GetProperty("multiPage").GetBoolean());
        Assert.True(row.GetProperty("vector").GetBoolean());
        Assert.Matches(@"vega-lite\s+chart\s+\.vl\.json \.vl\s+single\s+yes", textOut);
        Assert.Equal(SourceKind.VegaLite, SourceLoader.Probe(FixtureFactory.WriteSource(directory.Path, "vega-lite")).Format);
    }

    /// <summary>
    ///     Test that the JSON description of a chart has an <c>image</c> object with its laid-out size and a <c>chart</c>
    ///     object with what the specification declares, and that the text description is one line.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Info_VegaLiteWritesChartObject()
    {
        // Arrange: a chart that declares a title, two marks, a parameter, and inline and named data
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "rich.vl.json", RichChart);
        var (width, height) = NaturalSize(path);
        var bytes = new FileInfo(path).Length;

        // Act
        var json = RunInfo(true, path);
        var text = RunInfo(false, path);

        // Assert: the documented keys and exact layout
        var escaped = JsonSerializer.Serialize(path);
        var expected = "{\"schemaVersion\":1,\"ok\":true,\"command\":\"info\",\"input\":{\"path\":" + escaped
            + ",\"format\":\"vega-lite\",\"bytes\":" + bytes + "},\"image\":{\"width\":" + width + ",\"height\":" + height
            + ",\"channels\":4,\"hasAlpha\":true,\"canDecode\":true,\"frameCount\":1},"
            + "\"chart\":{\"title\":\"Info test\",\"schemaMajorVersion\":5,\"marks\":[\"bar\",\"rule\"],"
            + "\"dataReferences\":[{\"kind\":\"inline\",\"rows\":2,\"line\":7},{\"kind\":\"name\",\"line\":10}],"
            + "\"parameters\":[\"cutoff\"]}}";
        Assert.Equal(expected + Environment.NewLine, json);
        using var document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.TryGetProperty("model", out _));
        Assert.False(document.RootElement.TryGetProperty("document", out _));
        Assert.True(width > 100 && height > 50);

        // The text is one line with the size, the counts, the title, and the schema version
        Assert.Equal(
            $"{path}: vega-lite, {width}x{height} px (laid out), 2 marks, 2 data references, 1 parameter, title \"Info test\", schema v5, {bytes} bytes{Environment.NewLine}",
            text);
    }

    /// <summary>
    ///     Test that a chart without a title, a schema, or data is described without those keys, with zero counts, and
    ///     that a chart with an error and the second page of a chart are expected errors.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Info_ChartWithoutOptionalFacts_OmitsThemAndReportsErrors()
    {
        // Arrange: a chart of one tick mark without data, a broken chart, and a good one
        using var directory = new TempDirectory();
        var bare = FixtureFactory.WriteText(directory.Path, "bare.vl.json", "{\"mark\": \"tick\"}");
        var broken = FixtureFactory.WriteText(directory.Path, "broken.vl.json", "{\"mark\": ");
        var good = FixtureFactory.WriteSource(directory.Path, "vega-lite");
        using var context = Context.Create([]);
        var command = new DiscoveryCommands.Info();

        // Act
        var json = RunInfo(true, bare);
        var text = RunInfo(false, bare);
        var syntax = Assert.Throws<ToolException>(() => command.Run(context, [broken]));
        var page = Assert.Throws<ToolException>(() => command.Run(context, [good, "--page", "2"]));

        // Assert: no title and no schema version, empty lists, and zero counts in the text
        using var document = JsonDocument.Parse(json);
        var chart = document.RootElement.GetProperty("chart");
        Assert.False(chart.TryGetProperty("title", out _));
        Assert.False(chart.TryGetProperty("schemaMajorVersion", out _));
        Assert.Equal(["tick"], chart.GetProperty("marks").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(0, chart.GetProperty("dataReferences").GetArrayLength());
        Assert.Equal(0, chart.GetProperty("parameters").GetArrayLength());
        Assert.Contains(", 1 mark, 0 data references, 0 parameters, ", text);
        Assert.DoesNotContain("title", text);
        Assert.Equal(ErrorCodes.VegaLiteError, syntax.Code);
        Assert.Equal(ErrorCodes.PageOutOfRange, page.Code);
    }

    /// <summary>
    ///     Test that the usage text of the info command names Vega-Lite charts.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_InfoUsage_NamesVegaLite()
    {
        // Arrange / Act
        var usage = new DiscoveryCommands.Info().Usage;

        // Assert
        Assert.Contains("Vega-Lite", usage);
        Assert.Contains("canvasnet info chart.vl.json --json", usage);
    }
}
