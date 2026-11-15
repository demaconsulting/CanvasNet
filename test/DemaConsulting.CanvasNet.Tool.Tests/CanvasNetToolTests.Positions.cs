using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Tool.Tests;

/// <summary>
///     System-level tests of the CanvasNet Tool for feature positions, run as a real process via <c>dotnet</c>: a diagram
///     and a chart report the features <c>--positions</c> selects, in the pixels of the written picture.
/// </summary>
public partial class CanvasNetToolTests
{
    /// <summary>
    ///     Test that the tool, run as a real process, reports the positions of a flowchart node and a chart bar, each
    ///     pixel landing on the feature's paint in the written picture, and fails a strict render whose selector matches
    ///     nothing.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Positions_RenderAsProcess_ReportsFeaturePixels()
    {
        // Arrange: a flowchart with a filled node, and a bar chart
        using var directory = new TempDirectory();
        var flow = FixtureFactory.WriteText(directory.Path, "flow.mmd", "flowchart LR\n    A[One] --> B[Two]\n    classDef done fill:#cfc\n    class B done\n");
        var chart = FixtureFactory.WriteText(
            directory.Path,
            "bars.vl.json",
            "{\"data\":{\"values\":[{\"a\":\"A\",\"b\":3},{\"a\":\"B\",\"b\":5}]},\"mark\":\"bar\",\"encoding\":{\"x\":{\"field\":\"a\",\"type\":\"nominal\"},\"y\":{\"field\":\"b\",\"type\":\"quantitative\"}}}");
        var flowOutput = directory.Combine("flow.png");
        var chartOutput = directory.Combine("bars.png");
        var strictOutput = directory.Combine("strict.png");

        // Act
        var flowExitCode = Runner.Run(out var flowResult, "dotnet", _dllPath, "render", flow, "--positions", "node:B", "--scale", "2", "-o", flowOutput, "--json");
        var chartExitCode = Runner.Run(out var chartResult, "dotnet", _dllPath, "render", chart, "--positions", "mark:x=B", "-o", chartOutput, "--json");
        var strictExitCode = Runner.Run(out var strictResult, "dotnet", _dllPath, "render", flow, "--positions", "node:Zed", "--strict", "-o", strictOutput, "--json");

        // Assert: the node's pixel is on its fill, above its label
        Assert.True(flowExitCode == 0, flowResult);
        using var flowDocument = JsonDocument.Parse(flowResult);
        var node = Assert.Single(flowDocument.RootElement.GetProperty("positions").EnumerateArray());
        var box = node.GetProperty("box");
        using var flowImage = PngCodec.Load(flowOutput);
        var nodeX = (int)node.GetProperty("pixel")[0].GetDouble();
        var nodeY = (int)(box.GetProperty("y").GetDouble() + (box.GetProperty("height").GetDouble() * 0.2));
        Assert.Equal(new Rgba32(0xCC, 0xFF, 0xCC, 255), flowImage[nodeX, nodeY]);

        // Assert: the bar's pixel is on the bar
        Assert.True(chartExitCode == 0, chartResult);
        using var chartDocument = JsonDocument.Parse(chartResult);
        var bar = Assert.Single(chartDocument.RootElement.GetProperty("positions").EnumerateArray());
        using var chartImage = PngCodec.Load(chartOutput);
        Assert.Equal(new Rgba32(0x4C, 0x78, 0xA8, 255), chartImage[(int)bar.GetProperty("pixel")[0].GetDouble(), (int)bar.GetProperty("pixel")[1].GetDouble()]);

        // Assert: the strict render that matched nothing failed with its code and wrote nothing
        Assert.NotEqual(0, strictExitCode);
        Assert.Contains("positions-unmatched", strictResult, StringComparison.Ordinal);
        Assert.False(File.Exists(strictOutput));
    }
}
