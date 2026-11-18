using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Tool.Tests;

/// <summary>
///     System-level tests of the CanvasNet Tool for PlantUML diagrams, run as a real process via <c>dotnet</c> so that the
///     bundled PlantUML library is proven to load next to the tool assembly.
/// </summary>
public partial class CanvasNetToolTests
{
    /// <summary>
    ///     Test that the tool, run as a real process, lists the format, describes and renders a sequence diagram, and prints
    ///     its help topic by its alias.
    /// </summary>
    [Fact]
    public void CanvasNetTool_PlantUml_RenderAsProcess_DescribesAndWritesImage()
    {
        // Arrange: a two-participant sequence diagram and an output path
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "talk.puml", "@startuml\nAlice -> Bob : Hello\nBob --> Alice : Hi\n@enduml\n");
        var output = directory.Combine("talk.png");

        // Act: run formats, info, render, and help as real processes
        var formatsExitCode = Runner.Run(out var formats, "dotnet", _dllPath, "formats", "--json");
        var infoExitCode = Runner.Run(out var info, "dotnet", _dllPath, "info", input, "--json");
        var renderExitCode = Runner.Run(out var render, "dotnet", _dllPath, "render", input, "-o", output, "--strict", "--json");
        var helpExitCode = Runner.Run(out var help, "dotnet", _dllPath, "help", "puml");

        // Assert: the format is listed, and info names the format and the diagram
        Assert.Equal(0, formatsExitCode);
        Assert.Contains("\"format\":\"plantuml\"", formats, StringComparison.Ordinal);
        Assert.Equal(0, infoExitCode);
        using var infoDocument = JsonDocument.Parse(info);
        Assert.Equal("plantuml", infoDocument.RootElement.GetProperty("input").GetProperty("format").GetString());
        var diagram = infoDocument.RootElement.GetProperty("diagram");
        Assert.Equal(("sequence", true, 2, 2), (diagram.GetProperty("type").GetString(), diagram.GetProperty("supported").GetBoolean(), diagram.GetProperty("nodes").GetInt32(), diagram.GetProperty("edges").GetInt32()));
        var size = infoDocument.RootElement.GetProperty("image");

        // Assert: render writes an image of the size info reported, white around participants in PlantUML's fill
        Assert.Equal(0, renderExitCode);
        using var renderDocument = JsonDocument.Parse(render);
        Assert.Equal("plantuml", renderDocument.RootElement.GetProperty("input").GetProperty("format").GetString());
        using var image = PngCodec.Load(output);
        Assert.Equal((size.GetProperty("width").GetInt32(), size.GetProperty("height").GetInt32()), (image.Width, image.Height));
        Assert.Equal(new Rgba32(255, 255, 255, 255), image[0, 0]);
        var participantFill = new Rgba32(0xE2, 0xE2, 0xF0, 255);
        Assert.Contains(Enumerable.Range(0, image.Height), y => Enumerable.Range(0, image.Width).Any(x => image[x, y] == participantFill));

        // Assert: the help topic is found by its alias
        Assert.Equal(0, helpExitCode);
        Assert.StartsWith("# PlantUML diagrams", help, StringComparison.Ordinal);
    }
}
