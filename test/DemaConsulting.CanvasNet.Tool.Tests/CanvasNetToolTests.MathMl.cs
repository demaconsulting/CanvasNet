using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Tool.Tests;

/// <summary>
///     System-level tests of the CanvasNet Tool for MathML equations, run as a real process via <c>dotnet</c> so that the
///     bundled MathML library is proven to load next to the tool assembly.
/// </summary>
public partial class CanvasNetToolTests
{
    /// <summary>
    ///     Test that the tool, run as a real process, lists the format, describes and renders an equation, and prints its
    ///     help topic by its alias.
    /// </summary>
    [Fact]
    public void CanvasNetTool_MathMl_RenderAsProcess_DescribesAndWritesImage()
    {
        // Arrange: the quadratic formula and an output path
        using var directory = new TempDirectory();
        var input = directory.Combine("quadratic.mml");
        File.Copy(FixtureFactory.FixturePath(Path.Combine("mathml", "quadratic.mml")), input);
        var output = directory.Combine("quadratic.png");

        // Act: run formats, info, render, and help as real processes
        var formatsExitCode = Runner.Run(out var formats, "dotnet", _dllPath, "formats", "--json");
        var infoExitCode = Runner.Run(out var info, "dotnet", _dllPath, "info", input, "--json");
        var renderExitCode = Runner.Run(out var render, "dotnet", _dllPath, "render", input, "-o", output, "--strict", "--json");
        var helpExitCode = Runner.Run(out var help, "dotnet", _dllPath, "help", "equation");

        // Assert: the format is listed, and info names the format and the equation
        Assert.Equal(0, formatsExitCode);
        Assert.Contains("\"format\":\"mathml\"", formats, StringComparison.Ordinal);
        Assert.Equal(0, infoExitCode);
        using var infoDocument = JsonDocument.Parse(info);
        Assert.Equal("mathml", infoDocument.RootElement.GetProperty("input").GetProperty("format").GetString());
        var equation = infoDocument.RootElement.GetProperty("equation");
        Assert.Equal("block", equation.GetProperty("display").GetString());
        Assert.True(equation.GetProperty("elements").GetInt32() > 10);
        var size = infoDocument.RootElement.GetProperty("image");

        // Assert: render writes an image of the size info reported, white around dark ink, without a diagnostic
        Assert.Equal(0, renderExitCode);
        using var renderDocument = JsonDocument.Parse(render);
        Assert.Equal("mathml", renderDocument.RootElement.GetProperty("input").GetProperty("format").GetString());
        Assert.Empty(renderDocument.RootElement.GetProperty("diagnostics").EnumerateArray());
        using var image = PngCodec.Load(output);
        Assert.Equal((size.GetProperty("width").GetInt32(), size.GetProperty("height").GetInt32()), (image.Width, image.Height));
        Assert.Equal(new Rgba32(255, 255, 255, 255), image[0, 0]);
        Assert.Contains(Enumerable.Range(0, image.Height), y => Enumerable.Range(0, image.Width).Any(x => image[x, y].R < 64));

        // Assert: the help topic is found by its alias
        Assert.Equal(0, helpExitCode);
        Assert.StartsWith("# MathML equations", help, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the tool, run as a real process, reports an equation that is not well-formed XML as the error
    ///     <c>mathml-error</c> with exit code 1, the file, line, and column in the message, a hint, and no output file.
    /// </summary>
    [Fact]
    public void CanvasNetTool_MathMl_SyntaxError_IsMathMlErrorWithLocation()
    {
        // Arrange: an equation whose third line closes the wrong element
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(
            directory.Path,
            "broken.mml",
            "<math xmlns=\"http://www.w3.org/1998/Math/MathML\">\n  <mi>x</mi>\n  <mo>=</mi>\n</math>\n");
        var output = directory.Combine("broken.png");

        // Act: render as a real process, with and without --json
        var exitCode = Runner.Run(out var text, "dotnet", _dllPath, "render", input, "-o", output);
        var jsonExitCode = Runner.Run(out var json, "dotnet", _dllPath, "render", input, "-o", output, "--json");

        // Assert: the error line names the code, the file, and the line, and a hint follows
        Assert.Equal(1, exitCode);
        Assert.Contains("Error: mathml-error: MathML syntax error:", text, StringComparison.Ordinal);
        Assert.Contains("broken.mml:3:", text, StringComparison.Ordinal);
        Assert.Contains("Hint:", text, StringComparison.Ordinal);

        // Assert: the JSON failure document carries the code, and nothing was written
        Assert.Equal(1, jsonExitCode);
        Assert.Contains("\"ok\":false", json, StringComparison.Ordinal);
        Assert.Contains("\"code\":\"mathml-error\"", json, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }
}
