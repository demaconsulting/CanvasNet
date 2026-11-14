using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Unit tests for the RenderCommand class with <c>--positions</c>: the <c>positions</c> array of the JSON result and
///     its shape, the union of repeated selectors, the alias, the usage errors, the mapping through the pixel steps, the
///     unmatched warning, and the strict failure.
/// </summary>
[Collection("Sequential")]
public class RenderCommandPositionTests
{
    /// <summary>A flowchart with two filled nodes and an edge.</summary>
    private const string Flowchart =
        "flowchart LR\n    A[Start] --> B[End]\n    classDef done fill:#cfc,stroke:#393\n    class A,B done\n";

    /// <summary>A bar chart with two bars.</summary>
    private const string Bars =
        "{\"data\":{\"values\":[{\"a\":\"A\",\"b\":3},{\"a\":\"B\",\"b\":5}]},\"mark\":\"bar\"," +
        "\"encoding\":{\"x\":{\"field\":\"a\",\"type\":\"nominal\"},\"y\":{\"field\":\"b\",\"type\":\"quantitative\"}}}";

    /// <summary>
    ///     Writes the flowchart and renders it with the given arguments and <c>--json</c>.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <param name="arguments">The arguments after the input.</param>
    /// <returns>The exit code, the output, and the error output.</returns>
    private static (int ExitCode, string StdOut, string StdErr) RenderFlow(TempDirectory directory, params string[] arguments)
    {
        var input = directory.Combine("flow.mmd");
        File.WriteAllText(input, Flowchart);
        return ToolHost.Invoke(["render", input, .. arguments, "--json"]);
    }

    /// <summary>
    ///     Test the shape of an item: the members in order, the box and the pixel rounded to three decimals, and inside.
    /// </summary>
    [Fact]
    public void RenderCommand_Positions_Json_HasTheDocumentedShape()
    {
        // Arrange
        using var directory = new TempDirectory();

        // Act
        var (exitCode, stdOut, stdErr) = RenderFlow(directory, "--positions", "edge", "--positions", "node:B", "-o", directory.Combine("out.png"));

        // Assert: the node before the edge, whatever the order of the selectors
        Assert.True(exitCode == 0, stdErr + stdOut);
        using var document = JsonDocument.Parse(stdOut);
        var root = document.RootElement;
        Assert.False(root.GetProperty("positionsTruncated").GetBoolean());
        var items = root.GetProperty("positions").EnumerateArray().ToList();
        Assert.Equal(["node", "edge"], items.Select(i => i.GetProperty("kind").GetString()));
        Assert.Equal(["kind", "id", "text", "line", "box", "pixel", "inside"], items[0].EnumerateObject().Select(p => p.Name));
        Assert.Equal(["kind", "id", "line", "from", "to", "index", "box", "pixel", "inside"], items[1].EnumerateObject().Select(p => p.Name));
        var pixel = items[0].GetProperty("pixel");
        Assert.Equal(Math.Round(pixel[0].GetDouble(), 3), pixel[0].GetDouble());
        Assert.True(items[0].GetProperty("inside").GetBoolean());
        Assert.Equal(("A", "B", 0), (items[1].GetProperty("from").GetString(), items[1].GetProperty("to").GetString(), items[1].GetProperty("index").GetInt32()));
    }

    /// <summary>
    ///     Test that a node's pixel in the written image has the node's fill, through the <c>convert</c> alias, and that a
    ///     crop and a pad move it.
    /// </summary>
    [Fact]
    public void RenderCommand_Positions_ConvertCropAndPad_PixelOnTheNode()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = directory.Combine("flow.mmd");
        File.WriteAllText(input, Flowchart);
        var output = directory.Combine("out.png");

        // Act
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke("convert", input, "--positions", "node:A", "--crop", "4,2,80,60", "--pad", "10", "-o", output, "--json");

        // Assert: above the label of the node, its fill
        Assert.True(exitCode == 0, stdErr + stdOut);
        using var document = JsonDocument.Parse(stdOut);
        var item = Assert.Single(document.RootElement.GetProperty("positions").EnumerateArray());
        var x = item.GetProperty("pixel")[0].GetDouble();
        var box = item.GetProperty("box");
        var top = box.GetProperty("y").GetDouble() + (box.GetProperty("height").GetDouble() * 0.2);
        using var written = PngCodec.Load(output);
        Assert.Equal(new Rgba32(0xCC, 0xFF, 0xCC, 255), written[(int)x, (int)top]);
    }

    /// <summary>
    ///     Test that a chart reports its marks with their view and values, and that <c>--grid</c> keeps the positions.
    /// </summary>
    [Fact]
    public void RenderCommand_Positions_ChartWithGrid_ReportsMarkValues()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = directory.Combine("bars.vl.json");
        File.WriteAllText(input, Bars);

        // Act
        var plain = ToolHost.Invoke("render", input, "--positions", "mark:x=B", "-o", directory.Combine("plain.png"), "--json");
        var grid = ToolHost.Invoke("render", input, "--positions", "mark:x=B", "--grid", "50", "-o", directory.Combine("grid.png"), "--json");

        // Assert
        using var plainDocument = JsonDocument.Parse(plain.StdOut);
        using var gridDocument = JsonDocument.Parse(grid.StdOut);
        var item = Assert.Single(plainDocument.RootElement.GetProperty("positions").EnumerateArray());
        Assert.Equal(("bar-1", "", "bar"), (item.GetProperty("id").GetString(), item.GetProperty("view").GetString(), item.GetProperty("mark").GetString()));
        Assert.Equal(5, item.GetProperty("values").GetProperty("y").GetDouble());
        Assert.Equal(item.GetProperty("pixel").ToString(), gridDocument.RootElement.GetProperty("positions")[0].GetProperty("pixel").ToString());
    }

    /// <summary>
    ///     Test the usage errors: no <c>--json</c>, an input that is not a diagram or a chart, and a malformed selector.
    /// </summary>
    [Fact]
    public void RenderCommand_Positions_Misuse_IsUsageError()
    {
        // Arrange
        using var directory = new TempDirectory();
        var flow = directory.Combine("flow.mmd");
        File.WriteAllText(flow, Flowchart);
        var png = directory.Combine("in.png");
        using (var surface = new Surface(4, 4))
        {
            PngCodec.Save(surface, png);
        }

        // Act
        var noJson = ToolHost.Invoke("render", flow, "--positions", "all", "-o", directory.Combine("a.png"));
        var raster = ToolHost.Invoke("render", png, "--positions", "all", "-o", directory.Combine("b.png"), "--json");
        var malformed = ToolHost.Invoke("render", flow, "--positions", "all:x", "-o", directory.Combine("c.png"), "--json");

        // Assert
        Assert.All([noJson, raster, malformed], result => Assert.NotEqual(0, result.ExitCode));
        Assert.Contains("usage-error", noJson.StdErr, StringComparison.Ordinal);
        Assert.Contains("usage-error", raster.StdOut, StringComparison.Ordinal);
        Assert.Contains("'all:x'", malformed.StdOut, StringComparison.Ordinal);
        Assert.Contains("help positions selectors", malformed.StdOut, StringComparison.Ordinal);
        Assert.False(File.Exists(directory.Combine("a.png")));
    }

    /// <summary>
    ///     Test that a selector that matches nothing is a warning, and with <c>--strict</c> the error
    ///     <c>positions-unmatched</c> with no output file; and that without <c>--positions</c> the result has no positions.
    /// </summary>
    [Fact]
    public void RenderCommand_Positions_Unmatched_WarnsOrFailsStrict()
    {
        // Arrange
        using var directory = new TempDirectory();
        var strictOutput = directory.Combine("strict.png");

        // Act
        var warned = RenderFlow(directory, "--positions", "node:Zed", "-o", directory.Combine("warned.png"));
        var strict = RenderFlow(directory, "--positions", "node:Zed", "--strict", "-o", strictOutput);
        var without = RenderFlow(directory, "-o", directory.Combine("without.png"));

        // Assert
        Assert.Equal(0, warned.ExitCode);
        using var warnedDocument = JsonDocument.Parse(warned.StdOut);
        var warning = Assert.Single(warnedDocument.RootElement.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal(("warning", "positions-unmatched"), (warning.GetProperty("severity").GetString(), warning.GetProperty("code").GetString()));
        Assert.Empty(warnedDocument.RootElement.GetProperty("positions").EnumerateArray());
        Assert.NotEqual(0, strict.ExitCode);
        Assert.Contains("positions-unmatched", strict.StdOut, StringComparison.Ordinal);
        Assert.False(File.Exists(strictOutput));
        using var withoutDocument = JsonDocument.Parse(without.StdOut);
        Assert.False(withoutDocument.RootElement.TryGetProperty("positions", out _));
        Assert.False(withoutDocument.RootElement.TryGetProperty("positionsTruncated", out _));
    }

    /// <summary>
    ///     Test that <c>inspect</c> does not take <c>--positions</c>.
    /// </summary>
    [Fact]
    public void InspectCommand_Positions_IsNotAnOption()
    {
        // Arrange
        using var directory = new TempDirectory();
        var flow = directory.Combine("flow.mmd");
        File.WriteAllText(flow, Flowchart);

        // Act
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke("inspect", "colors", flow, "--positions", "all", "--json");

        // Assert
        Assert.NotEqual(0, exitCode);
        Assert.Contains("--positions", stdOut + stdErr, StringComparison.Ordinal);
    }
}
