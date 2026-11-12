using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Tool.Tests;

/// <summary>
///     System-level tests of the CanvasNet Tool for Mermaid diagrams, run as a real process via <c>dotnet</c> so that the
///     bundled Mermaid library is proven to load next to the tool assembly.
/// </summary>
public partial class CanvasNetToolTests
{
    /// <summary>
    ///     Test that the tool, run as a real process, lists the format, describes and renders a flowchart, and prints its help
    ///     topic.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Mermaid_RenderAsProcess_DescribesAndWritesImage()
    {
        // Arrange: a three-node flowchart and an output path
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "flow.mmd", "flowchart LR\n    A[One] --> B[Two] --> C[Three]\n");
        var output = directory.Combine("flow.png");

        // Act: run formats, info, render, and help as real processes
        var formatsExitCode = Runner.Run(out var formats, "dotnet", _dllPath, "formats", "--json");
        var infoExitCode = Runner.Run(out var info, "dotnet", _dllPath, "info", input, "--json");
        var renderExitCode = Runner.Run(out var render, "dotnet", _dllPath, "render", input, "-o", output, "--json");
        var helpExitCode = Runner.Run(out var help, "dotnet", _dllPath, "help", "mmd");

        // Assert: the format is listed, and info names the format and the diagram
        Assert.Equal(0, formatsExitCode);
        Assert.Contains("\"format\":\"mermaid\"", formats, StringComparison.Ordinal);
        Assert.Equal(0, infoExitCode);
        using var infoDocument = JsonDocument.Parse(info);
        Assert.Equal("mermaid", infoDocument.RootElement.GetProperty("input").GetProperty("format").GetString());
        var diagram = infoDocument.RootElement.GetProperty("diagram");
        Assert.Equal(("flowchart", true, 3, 2), (diagram.GetProperty("type").GetString(), diagram.GetProperty("supported").GetBoolean(), diagram.GetProperty("nodes").GetInt32(), diagram.GetProperty("edges").GetInt32()));
        var size = infoDocument.RootElement.GetProperty("image");

        // Assert: render writes an image of the size info reported, white around the nodes of the default theme
        Assert.Equal(0, renderExitCode);
        using var renderDocument = JsonDocument.Parse(render);
        Assert.Equal("mermaid", renderDocument.RootElement.GetProperty("input").GetProperty("format").GetString());
        using var image = PngCodec.Load(output);
        Assert.Equal((size.GetProperty("width").GetInt32(), size.GetProperty("height").GetInt32()), (image.Width, image.Height));
        Assert.Equal(new Rgba32(255, 255, 255, 255), image[0, 0]);
        Assert.Equal(new Rgba32(0xEC, 0xEC, 0xFF, 255), image[14, image.Height / 2]);

        // Assert: the help topic is found by its alias
        Assert.Equal(0, helpExitCode);
        Assert.StartsWith("Topic: Mermaid diagrams", help, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the tool, run as a real process, describes and draws a pie chart and a gantt chart strictly, each at the
    ///     size info reports and without a diagnostic.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Mermaid_PieAndGanttAsProcess_AreDrawn()
    {
        // Arrange: a pie chart and a gantt chart
        using var directory = new TempDirectory();
        var pie = FixtureFactory.WriteText(directory.Path, "pets.mmd", "pie title Pets\n    \"Dogs\" : 386\n    \"Cats\" : 85\n");
        var gantt = FixtureFactory.WriteText(directory.Path, "plan.mmd", "gantt\n    dateFormat YYYY-MM-DD\n    section Build\n    Design :des, 2024-06-03, 5d\n    Implement :after des, 8d\n");

        foreach (var (input, type, nodes) in new[] { (pie, "pie", 2), (gantt, "gantt", 2) })
        {
            // Act: run info and a strict render as real processes
            var output = Path.ChangeExtension(input, ".png");
            var infoExitCode = Runner.Run(out var info, "dotnet", _dllPath, "info", input, "--json");
            var renderExitCode = Runner.Run(out var render, "dotnet", _dllPath, "render", input, "-o", output, "--strict", "--json");

            // Assert: info names the supported type and its nodes; render writes that size without a diagnostic
            Assert.Equal(0, infoExitCode);
            using var infoDocument = JsonDocument.Parse(info);
            var diagram = infoDocument.RootElement.GetProperty("diagram");
            Assert.Equal((type, true, nodes), (diagram.GetProperty("type").GetString(), diagram.GetProperty("supported").GetBoolean(), diagram.GetProperty("nodes").GetInt32()));
            var size = infoDocument.RootElement.GetProperty("image");
            Assert.Equal(0, renderExitCode);
            using var renderDocument = JsonDocument.Parse(render);
            Assert.Empty(renderDocument.RootElement.GetProperty("diagnostics").EnumerateArray());
            using var image = PngCodec.Load(output);
            Assert.Equal((size.GetProperty("width").GetInt32(), size.GetProperty("height").GetInt32()), (image.Width, image.Height));
            Assert.Equal(new Rgba32(255, 255, 255, 255), image[0, 0]);
        }
    }

    /// <summary>
    ///     Test that the tool, run as a real process, describes and draws a mindmap strictly, at the size info reports and
    ///     without a diagnostic.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Mermaid_MindmapAsProcess_IsDrawn()
    {
        // Arrange: a mindmap of three nodes
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "garden.mmd", "mindmap\n  root((Garden))\n    Soil\n    Plants\n");
        var output = directory.Combine("garden.png");

        // Act: run info and a strict render as real processes
        var infoExitCode = Runner.Run(out var info, "dotnet", _dllPath, "info", input, "--json");
        var renderExitCode = Runner.Run(out var render, "dotnet", _dllPath, "render", input, "-o", output, "--strict", "--json");

        // Assert: info names the supported type, its nodes and edges; render writes that size without a diagnostic
        Assert.Equal(0, infoExitCode);
        using var infoDocument = JsonDocument.Parse(info);
        var diagram = infoDocument.RootElement.GetProperty("diagram");
        Assert.Equal(("mindmap", true, 3, 2), (diagram.GetProperty("type").GetString(), diagram.GetProperty("supported").GetBoolean(), diagram.GetProperty("nodes").GetInt32(), diagram.GetProperty("edges").GetInt32()));
        var size = infoDocument.RootElement.GetProperty("image");
        Assert.Equal(0, renderExitCode);
        using var renderDocument = JsonDocument.Parse(render);
        Assert.Empty(renderDocument.RootElement.GetProperty("diagnostics").EnumerateArray());
        using var image = PngCodec.Load(output);
        Assert.Equal((size.GetProperty("width").GetInt32(), size.GetProperty("height").GetInt32()), (image.Width, image.Height));
        Assert.Equal(new Rgba32(255, 255, 255, 255), image[0, 0]);
    }

    /// <summary>
    ///     Test that the tool, run as a real process, describes and draws a packet diagram strictly, at the size info reports
    ///     (the width of 32 bits of 32 pixels plus 2, the height of two rows of blocks) and without a diagnostic.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Mermaid_PacketAsProcess_IsDrawn()
    {
        // Arrange: a packet diagram of three blocks over two rows
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "frame.mmd", "packet\n    0-15: \"Source\"\n    +16: \"Target\"\n    +8: \"Flags\"\n");
        var output = directory.Combine("frame.png");

        // Act: run info and a strict render as real processes
        var infoExitCode = Runner.Run(out var info, "dotnet", _dllPath, "info", input, "--json");
        var renderExitCode = Runner.Run(out var render, "dotnet", _dllPath, "render", input, "-o", output, "--strict", "--json");

        // Assert: info names the supported type and its blocks; render writes that size without a diagnostic
        Assert.Equal(0, infoExitCode);
        using var infoDocument = JsonDocument.Parse(info);
        var diagram = infoDocument.RootElement.GetProperty("diagram");
        Assert.Equal(("packet", true, 3, 0), (diagram.GetProperty("type").GetString(), diagram.GetProperty("supported").GetBoolean(), diagram.GetProperty("nodes").GetInt32(), diagram.GetProperty("edges").GetInt32()));
        var size = infoDocument.RootElement.GetProperty("image");
        Assert.Equal((1026, 109), (size.GetProperty("width").GetInt32(), size.GetProperty("height").GetInt32()));
        Assert.Equal(0, renderExitCode);
        using var renderDocument = JsonDocument.Parse(render);
        Assert.Empty(renderDocument.RootElement.GetProperty("diagnostics").EnumerateArray());
        using var image = PngCodec.Load(output);
        Assert.Equal((1026, 109), (image.Width, image.Height));
        Assert.Equal(new Rgba32(255, 255, 255, 255), image[0, 0]);
    }

    /// <summary>
    ///     Test that an unknown diagram keyword is a <c>mermaid-error</c> with a hint, that a diagram type this version does not
    ///     draw is a diagnostic and an <c>unsupported-feature</c> error with <c>--strict</c>, and that <c>--theme</c> on another
    ///     kind of input is a usage error, each with the documented exit code.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Mermaid_ErrorsAndDiagnostics_FollowTheContract()
    {
        // Arrange
        using var directory = new TempDirectory();
        var unknown = FixtureFactory.WriteText(directory.Path, "unknown.mmd", "drawing LR\n    a --> b\n");
        var journey = FixtureFactory.WriteText(directory.Path, "zoo.mmd", "journey\n    title Ducks\n");
        var svg = FixtureFactory.WriteSource(directory.Path, "svg");

        // Act
        var (unknownExit, _, unknownErr) = ToolHost.Invoke("render", unknown, "-o", directory.Combine("u.png"));
        var (journeyExit, _, journeyErr) = ToolHost.Invoke("render", journey, "-o", directory.Combine("s.png"));
        var (strictExit, _, strictErr) = ToolHost.Invoke("render", journey, "--strict", "-o", directory.Combine("t.png"));
        var (themeExit, _, themeErr) = ToolHost.Invoke("render", svg, "--theme", "dark", "-o", directory.Combine("v.png"));

        // Assert
        Assert.Equal(1, unknownExit);
        Assert.Contains("Error: mermaid-error:", unknownErr, StringComparison.Ordinal);
        Assert.Contains("mermaid-unknown-diagram", unknownErr, StringComparison.Ordinal);
        Assert.Contains("Hint: ", unknownErr, StringComparison.Ordinal);
        Assert.Equal(0, journeyExit);
        Assert.Contains("Unsupported: mermaid-diagram-unsupported:", journeyErr, StringComparison.Ordinal);
        Assert.Equal(1, strictExit);
        Assert.Contains("Error: unsupported-feature:", strictErr, StringComparison.Ordinal);
        Assert.Equal(1, themeExit);
        Assert.Contains("Error: usage-error:", themeErr, StringComparison.Ordinal);
    }
}
