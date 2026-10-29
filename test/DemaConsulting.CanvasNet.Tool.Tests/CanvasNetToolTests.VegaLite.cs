using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Tool.Tests;

/// <summary>
///     System-level tests of the CanvasNet Tool for Vega-Lite charts, run as a real process via <c>dotnet</c> so that
///     the bundled Vega-Lite library is proven to load next to the tool assembly.
/// </summary>
public partial class CanvasNetToolTests
{
    /// <summary>
    ///     Test that the tool, run as a real process, describes and renders a chart, and reports what it drew.
    /// </summary>
    [Fact]
    public void CanvasNetTool_VegaLite_RenderAsProcess_DescribesAndWritesImage()
    {
        // Arrange: the two-bar chart and an output path
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "bars.vl.json", FixtureFactory.TwoBarChart);
        var output = directory.Combine("bars.png");

        // Act: run the info and render commands as real processes
        var infoExitCode = Runner.Run(out var info, "dotnet", _dllPath, "info", input, "--json");
        var renderExitCode = Runner.Run(out var render, "dotnet", _dllPath, "render", input, "-o", output, "--json");

        // Assert: info names the format and the chart, and render writes an image of the size info reported
        Assert.Equal(0, infoExitCode);
        using var infoDocument = JsonDocument.Parse(info);
        Assert.Equal("vega-lite", infoDocument.RootElement.GetProperty("input").GetProperty("format").GetString());
        var chart = infoDocument.RootElement.GetProperty("chart");
        Assert.Equal(["bar"], chart.GetProperty("marks").EnumerateArray().Select(e => e.GetString()));
        var size = infoDocument.RootElement.GetProperty("image");
        Assert.Equal(0, renderExitCode);
        using var renderDocument = JsonDocument.Parse(render);
        Assert.Equal("vega-lite", renderDocument.RootElement.GetProperty("input").GetProperty("format").GetString());
        Assert.Equal("render", renderDocument.RootElement.GetProperty("operations")[0].GetProperty("op").GetString());
        using var image = PngCodec.Load(output);
        Assert.Equal((size.GetProperty("width").GetInt32(), size.GetProperty("height").GetInt32()), (image.Width, image.Height));
        Assert.Equal(new Rgba32(255, 255, 255, 255), image[0, 0]);
        Assert.Equal(new Rgba32(0x4C, 0x78, 0xA8, 255), image[image.Width - 5 - 50, 15]);
    }

    /// <summary>
    ///     Test that a chart is detected by its suffix and by its schema, that unsupported constructs are reported as
    ///     diagnostics and fail with <c>--strict</c>, and that a chart with an error is a <c>vega-lite-error</c>.
    /// </summary>
    [Fact]
    public void CanvasNetTool_VegaLite_DetectionDiagnosticsAndErrors_FollowTheContract()
    {
        // Arrange: a chart named .json (found by its schema), one with an unsupported mark, a broken chart, and Vega
        using var directory = new TempDirectory();
        var bySchema = FixtureFactory.WriteText(directory.Path, "bars.json", FixtureFactory.TwoBarChart);
        var arcs = FixtureFactory.WriteText(
            directory.Path,
            "arcs.vl.json",
            "{\"data\": {\"values\": [{\"v\": 1}]}, \"mark\": \"arc\", \"encoding\": {\"theta\": {\"field\": \"v\", \"type\": \"quantitative\"}}}");
        var broken = FixtureFactory.WriteText(directory.Path, "broken.vl.json", "{\"mark\": ");
        var vega = FixtureFactory.WriteText(directory.Path, "vega.json", "{\"$schema\": \"https://vega.github.io/schema/vega/v5.json\"}");

        // Act
        var (schemaExit, schemaOut, _) = ToolHost.Invoke("info", bySchema, "--json");
        var (arcExit, arcOut, _) = ToolHost.Invoke("render", arcs, "-o", directory.Combine("arcs.png"), "--json");
        var (strictExit, _, strictErr) = ToolHost.Invoke("render", arcs, "--strict", "-o", directory.Combine("strict.png"));
        var (brokenExit, brokenOut, brokenErr) = ToolHost.Invoke("render", broken, "-o", directory.Combine("broken.png"), "--json");
        var (vegaExit, _, vegaErr) = ToolHost.Invoke("info", vega);

        // Assert: found by its schema; reported and not skipped silently; the stable codes for the failures; no output for them
        Assert.Equal(0, schemaExit);
        using (var schemaDocument = JsonDocument.Parse(schemaOut))
        {
            Assert.Equal("vega-lite", schemaDocument.RootElement.GetProperty("input").GetProperty("format").GetString());
        }

        Assert.Equal(0, arcExit);
        using (var arcDocument = JsonDocument.Parse(arcOut))
        {
            Assert.Contains("vl-mark-arc", arcDocument.RootElement.GetProperty("diagnostics").EnumerateArray().Select(d => d.GetProperty("code").GetString()));
        }

        Assert.Equal(1, strictExit);
        Assert.Contains("Error: unsupported-feature:", strictErr);
        Assert.False(File.Exists(directory.Combine("strict.png")));
        Assert.Equal(1, brokenExit);
        Assert.Contains("Error: vega-lite-error:", brokenErr);
        using (var brokenDocument = JsonDocument.Parse(brokenOut))
        {
            Assert.Equal("vega-lite-error", brokenDocument.RootElement.GetProperty("error").GetProperty("code").GetString());
        }

        Assert.False(File.Exists(directory.Combine("broken.png")));
        Assert.Equal(1, vegaExit);
        Assert.Contains("Error: unsupported-format:", vegaErr);
    }
}
