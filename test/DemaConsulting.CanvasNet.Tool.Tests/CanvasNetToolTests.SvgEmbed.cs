// cspell:ignore scad svg href
using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Tool.Tests;

/// <summary>
///     System-level tests of the CanvasNet Tool for SVG diagrams that embed other files, run as a real process via
///     <c>dotnet</c> so that the SVG, SCAD, and Vega-Lite libraries are proven to work together next to the tool
///     assembly.
/// </summary>
public partial class CanvasNetToolTests
{
    /// <summary>
    ///     Writes the diagram of the user guide: a model and a chart beside it, and a green picture outside its folder.
    /// </summary>
    /// <param name="directory">The temporary directory: the diagram is written to its <c>site</c> folder.</param>
    /// <returns>The path of the diagram.</returns>
    private static string WriteGuideDiagram(TempDirectory directory)
    {
        Directory.CreateDirectory(directory.Combine("site"));
        File.Copy(FixtureFactory.FixturePath("cube.scad"), directory.Combine("site", "part.scad"));
        File.Copy(FixtureFactory.FixturePath("baskets.vl.json"), directory.Combine("site", "baskets.vl.json"));
        using (var surface = new Surface(4, 4))
        {
            surface.Clear(new Rgba32(0, 255, 0, 255));
            PngCodec.Save(surface, directory.Combine("outside.png"));
        }

        return FixtureFactory.WriteText(
            directory.Combine("site"),
            "diagram.svg",
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 240 120' width='240' height='120'>"
            + "<rect width='240' height='120' fill='#ff0000'/>"
            + "<image href='part.scad' x='0' y='0' width='120' height='120'/>"
            + "<image href='baskets.vl.json' x='120' y='0' width='120' height='60'/>"
            + "<image href='../outside.png' x='120' y='60' width='60' height='60'/>"
            + "</svg>");
    }

    /// <summary>
    ///     Test that the tool, run as a real process with <c>--file-access roots</c>, renders a diagram that embeds an
    ///     OpenSCAD model and a Vega-Lite chart next to it, the example of the user guide, and warns about a picture
    ///     outside the folder, which it does not draw.
    /// </summary>
    [Fact]
    public void CanvasNetTool_SvgEmbed_RenderAsProcess_DrawsModelAndChartAndWarnsOutsideFolder()
    {
        // Arrange: the diagram of the user guide with a model, a chart, and one picture outside the folder
        using var directory = new TempDirectory();
        var input = WriteGuideDiagram(directory);
        var output = directory.Combine("diagram.png");

        // Act
        var exitCode = Runner.Run(out var render, "dotnet", _dllPath, "render", input, "--file-access", "roots", "-o", output, "--json");

        // Assert: it succeeded, drew the chart opaque and the model over the diagram, and warned about the denied picture
        Assert.Equal(0, exitCode);
        using var document = JsonDocument.Parse(render);
        var diagnostic = Assert.Single(document.RootElement.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal("image-access-denied", diagnostic.GetProperty("code").GetString());
        using var image = PngCodec.Load(output);
        Assert.Equal((240, 120), (image.Width, image.Height));
        Assert.Equal(new Rgba32(255, 0, 0, 255), image[0, 0]);
        Assert.Equal(new Rgba32(255, 255, 255, 255), image[125, 5]);
        Assert.Equal(new Rgba32(255, 0, 0, 255), image[150, 90]);
    }

    /// <summary>
    ///     Test that the tool, run as a real process under the default file-access mode, draws the picture outside the
    ///     folder of the diagram as Pandoc would, with no diagnostic.
    /// </summary>
    [Fact]
    public void CanvasNetTool_SvgEmbed_RenderAsProcess_DrawsAnOutsidePictureUnderTheDefaultPolicy()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = WriteGuideDiagram(directory);
        var output = directory.Combine("diagram.png");

        // Act
        var exitCode = Runner.Run(out var render, "dotnet", _dllPath, "render", input, "-o", output, "--json");

        // Assert: no note, and the outside picture is drawn
        Assert.Equal(0, exitCode);
        using var document = JsonDocument.Parse(render);
        Assert.Empty(document.RootElement.GetProperty("diagnostics").EnumerateArray());
        using var image = PngCodec.Load(output);
        Assert.Equal(new Rgba32(0, 255, 0, 255), image[150, 90]);
    }
}
