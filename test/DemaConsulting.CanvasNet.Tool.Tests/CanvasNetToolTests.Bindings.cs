using System.Text.Json;

namespace DemaConsulting.CanvasNet.Tool.Tests;

/// <summary>
///     System-level tests of the CanvasNet Tool for SVG position bindings, run as a real process via <c>dotnet</c>: a figure
///     whose arrow is bound to a node of an embedded diagram is rendered and flattened, and a binding that matches nothing
///     fails a strict render before anything is written.
/// </summary>
public partial class CanvasNetToolTests
{
    /// <summary>
    ///     Test that the tool, run as a real process, resolves a binding in a strict render (the operation <c>bind</c>),
    ///     bakes it into a flattened copy (<c>bindings.resolved</c>), and fails a strict render whose binding matches
    ///     nothing with <c>svg-binding-failed</c> and no output.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Bindings_RenderAndFlattenAsProcess_ResolveTheBinding()
    {
        // Arrange: a figure with an arrow bound to node B of a flowchart, and one bound to a node that does not exist
        using var directory = new TempDirectory();
        FixtureFactory.WriteText(directory.Path, "flow.mmd", "flowchart LR\n    A[One] --> B[Two]\n");
        string Figure(string name, string node) => FixtureFactory.WriteText(
            directory.Path,
            name,
            "<svg xmlns='http://www.w3.org/2000/svg' xmlns:cn='https://demaconsulting.com/canvasnet' width='400' height='200'>"
            + "<image id='flow' href='flow.mmd' x='20' y='30' width='300' height='120'/>"
            + $"<line x1='0' y1='0' x2='1' y2='1' stroke='black' cn:x2y2='#flow node:{node}'/></svg>");
        var good = Figure("good.svg", "B");
        var bad = Figure("bad.svg", "Zed");
        var badOutput = directory.Combine("bad.png");

        // Act
        var renderExitCode = Runner.Run(out var renderResult, "dotnet", _dllPath, "render", good, "-o", directory.Combine("good.png"), "--strict", "--json");
        var flattenExitCode = Runner.Run(out var flattenResult, "dotnet", _dllPath, "flatten", good, "-o", directory.Combine("flat.svg"), "--strict", "--json");
        var strictExitCode = Runner.Run(out var strictResult, "dotnet", _dllPath, "render", bad, "-o", badOutput, "--strict", "--json");

        // Assert: the render bound the line to node B, inside the image
        Assert.True(renderExitCode == 0, renderResult);
        using var render = JsonDocument.Parse(renderResult);
        var bind = Assert.Single(render.RootElement.GetProperty("operations").EnumerateArray(), o => o.GetProperty("op").GetString() == "bind");
        Assert.Equal("node:B", bind.GetProperty("selector").GetString());
        Assert.InRange(bind.GetProperty("x").GetDouble(), 20, 320);
        Assert.InRange(bind.GetProperty("y").GetDouble(), 30, 150);

        // Assert: the copy has the binding baked, with the numbers of the render
        Assert.True(flattenExitCode == 0, flattenResult);
        using var flatten = JsonDocument.Parse(flattenResult);
        Assert.Equal(1, flatten.RootElement.GetProperty("bindings").GetProperty("resolved").GetInt32());
        var copy = File.ReadAllText(directory.Combine("flat.svg"));
        Assert.DoesNotContain("canvasnet", copy, StringComparison.Ordinal);
        Assert.Contains($"x2='{bind.GetProperty("x").GetDouble().ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}'", copy, StringComparison.Ordinal);

        // Assert: the strict render that matched nothing failed with its code and wrote nothing
        Assert.NotEqual(0, strictExitCode);
        Assert.Contains("svg-binding-failed", strictResult, StringComparison.Ordinal);
        Assert.False(File.Exists(badOutput));
    }
}
