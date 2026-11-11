using System.Text.Json;

namespace DemaConsulting.CanvasNet.Tool.Tests;

/// <summary>
///     System-level tests of the <c>flatten</c> command of the CanvasNet Tool, run as a real process via <c>dotnet</c>, so
///     that the SVG library, the format libraries, and the viewer are proven to work together next to the tool assembly.
/// </summary>
public partial class CanvasNetToolTests
{
    /// <summary>
    ///     Test that the tool, run as a real process, flattens a drawing that links a model and a raster: the model becomes
    ///     a picture, the raster link stays, and the exit code is 0.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Flatten_CompositeSvg_WritesFlattenedCopy()
    {
        // Arrange
        using var directory = new TempDirectory();
        File.Copy(FixtureFactory.FixturePath("cube.scad"), directory.Combine("part.scad"));
        var input = FixtureFactory.WriteText(
            directory.Path,
            "diagram.svg",
            "<svg xmlns='http://www.w3.org/2000/svg' width='120' height='60'>"
            + "<image href='part.scad' width='60' height='60'/><image href='logo.png' x='60' width='60' height='60'/></svg>");
        var output = directory.Combine("flat.svg");

        // Act
        var exitCode = Runner.Run(out var result, "dotnet", _dllPath, "flatten", input, "-o", output, "--json");

        // Assert
        Assert.Equal(0, exitCode);
        using var document = JsonDocument.Parse(result);
        Assert.True(document.RootElement.GetProperty("flattened").GetBoolean());
        Assert.Equal("scad", Assert.Single(document.RootElement.GetProperty("replaced").EnumerateArray()).GetProperty("format").GetString());
        var text = File.ReadAllText(output);
        Assert.Contains("href='data:image/png;base64,", text, StringComparison.Ordinal);
        Assert.Contains("href='logo.png'", text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the tool, run as a real process, exits with 0 and writes nothing for a drawing with nothing to flatten.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Flatten_PlainSvg_ExitsZeroWithoutOutput()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "plain.svg", "<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'/>");
        var output = directory.Combine("flat.svg");

        // Act
        var exitCode = Runner.Run(out var result, "dotnet", _dllPath, "flatten", input, "-o", output);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.StartsWith("nothing to flatten", result, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    /// <summary>
    ///     Test that the tool, run as a real process, reports the failures of flatten as expected errors with exit code 1.
    /// </summary>
    [Fact]
    public void CanvasNetTool_Flatten_Errors_AreReportedAsExpectedErrors()
    {
        // Arrange: a drawing that links a missing model
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(
            directory.Path,
            "diagram.svg",
            "<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'><image href='missing.scad' width='10' height='10'/></svg>");

        // Act
        var strict = Runner.Run(out var strictResult, "dotnet", _dllPath, "flatten", input, "-o", directory.Combine("flat.svg"), "--strict", "--json");
        var usage = Runner.Run(out var usageResult, "dotnet", _dllPath, "flatten", input, "--json");

        // Assert: the JSON failure document is the first line; the error is also on standard error
        Assert.Equal(1, strict);
        using var strictDocument = JsonDocument.Parse(strictResult.Split('\n')[0]);
        Assert.Equal("unsupported-feature", strictDocument.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(1, usage);
        using var usageDocument = JsonDocument.Parse(usageResult.Split('\n')[0]);
        Assert.Equal("usage-error", usageDocument.RootElement.GetProperty("error").GetProperty("code").GetString());
    }
}
