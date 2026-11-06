// cspell:ignore scad svg href
using System.Text.Json;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Tests of the <c>detail</c>, <c>location</c>, and <c>origin</c> members that the JSON diagnostics of the
///     <c>render</c>, <c>inspect</c>, <c>compare</c>, and <c>montage</c> commands carry, and of their absence from a
///     plain note.
/// </summary>
[Collection("Sequential")]
public class RenderCommandDiagnosticDetailTests
{
    /// <summary>A model with one malformed anchor, which the library reports as <c>scad-anchor-invalid</c>.</summary>
    private const string ModelWithBadAnchor = "cube(5);\necho(\"anchor\", \"bad\", [1, 2]);\n";

    /// <summary>A chart with an unknown top-level property, which the library reports as <c>vl-unknown-property</c>.</summary>
    private const string ChartWithUnknownProperty =
        "{\"data\":{\"values\":[{\"x\":1,\"y\":2},{\"x\":2,\"y\":3}]},\"mark\":\"point\","
        + "\"encoding\":{\"x\":{\"field\":\"x\",\"type\":\"quantitative\"},\"y\":{\"field\":\"y\",\"type\":\"quantitative\"}},\"bogus\":1}";

    /// <summary>
    ///     Test that <c>render</c> writes the library identifier as <c>detail</c> and the position as <c>location</c> for
    ///     a model warning, while <c>code</c> stays the generic one.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_ScadWarning_WritesDetailAndLocation()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "model.scad", ModelWithBadAnchor);

        // Act
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke("render", input, "--size", "64x64", "-o", directory.Combine("out.png"), "--json");

        // Assert
        Assert.True(exitCode == 0, stdErr + stdOut);
        using var document = JsonDocument.Parse(stdOut);
        var diagnostic = Assert.Single(document.RootElement.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal("scad-warning", diagnostic.GetProperty("code").GetString());
        Assert.Equal("scad-anchor-invalid", diagnostic.GetProperty("detail").GetString());
        var location = diagnostic.GetProperty("location");
        Assert.EndsWith("model.scad", location.GetProperty("file").GetString(), StringComparison.Ordinal);
        Assert.Equal(2, location.GetProperty("line").GetInt32());
        Assert.False(diagnostic.TryGetProperty("origin", out _));
    }

    /// <summary>
    ///     Test that <c>render</c> writes the position of an unsupported model feature (the code is the feature itself,
    ///     so there is no <c>detail</c>).
    /// </summary>
    [Fact]
    public void RenderCommand_Run_ScadUnsupportedFeature_WritesLocationWithoutDetail()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "model.scad", "cube(5);\noffset(r=1) square(2);\n");

        // Act
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke("render", input, "--size", "64x64", "-o", directory.Combine("out.png"), "--json");

        // Assert
        Assert.True(exitCode == 0, stdErr + stdOut);
        using var document = JsonDocument.Parse(stdOut);
        var diagnostic = Assert.Single(document.RootElement.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal("scad-module-offset", diagnostic.GetProperty("code").GetString());
        Assert.False(diagnostic.TryGetProperty("detail", out _));
        Assert.Equal(2, diagnostic.GetProperty("location").GetProperty("line").GetInt32());
    }

    /// <summary>
    ///     Test that <c>render</c> writes the <c>vl-*</c> identifier of a chart warning as <c>detail</c>.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_VegaLiteWarning_WritesDetail()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "chart.vl.json", ChartWithUnknownProperty);

        // Act
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke("render", input, "-o", directory.Combine("out.png"), "--json");

        // Assert
        Assert.True(exitCode == 0, stdErr + stdOut);
        using var document = JsonDocument.Parse(stdOut);
        var diagnostic = Assert.Single(document.RootElement.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal("vega-lite-warning", diagnostic.GetProperty("code").GetString());
        Assert.Equal("vl-unknown-property", diagnostic.GetProperty("detail").GetString());
    }

    /// <summary>
    ///     Test that a note of an embedded model carries <c>detail</c>, <c>location</c>, and the embedding chain as
    ///     <c>origin</c>, and that a link of the SVG file itself that was not drawn is a plain note (no <c>detail</c>,
    ///     <c>location</c>, or <c>origin</c>), while the same link in a nested SVG file names that file as <c>origin</c>.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_SvgEmbeddingNotes_WriteOriginAndOnlyWhatIsKnown()
    {
        // Arrange
        using var directory = new TempDirectory();
        FixtureFactory.WriteText(directory.Path, "part.scad", ModelWithBadAnchor);
        FixtureFactory.WriteText(
            directory.Path,
            "inner.svg",
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 40 40' width='40' height='40'>"
            + "<image href='gone.png' x='0' y='0' width='10' height='10'/></svg>");
        var input = FixtureFactory.WriteText(
            directory.Path,
            "diagram.svg",
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 120 80' width='120' height='80'>"
            + "<image href='part.scad' x='0' y='0' width='40' height='40'/>"
            + "<image href='inner.svg' x='40' y='0' width='40' height='40'/>"
            + "<image href='missing.png' x='80' y='0' width='40' height='40'/></svg>");

        // Act
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke("render", input, "-o", directory.Combine("out.png"), "--json");

        // Assert
        Assert.True(exitCode == 0, stdErr + stdOut);
        using var document = JsonDocument.Parse(stdOut);
        var diagnostics = document.RootElement.GetProperty("diagnostics").EnumerateArray().ToList();
        var note = Assert.Single(diagnostics, d => d.GetProperty("code").GetString() == "scad-warning");
        Assert.Equal("scad-anchor-invalid", note.GetProperty("detail").GetString());
        Assert.Equal(2, note.GetProperty("location").GetProperty("line").GetInt32());
        Assert.Equal(["part.scad"], note.GetProperty("origin").EnumerateArray().Select(e => e.GetString()).ToList());
        var unresolved = Assert.Single(diagnostics, d => d.GetProperty("message").GetString()!.Contains("'missing.png'", StringComparison.Ordinal));
        Assert.Equal("svg-image-unresolved", unresolved.GetProperty("code").GetString());
        Assert.False(unresolved.TryGetProperty("detail", out _));
        Assert.False(unresolved.TryGetProperty("location", out _));
        Assert.False(unresolved.TryGetProperty("origin", out _));
        var nested = Assert.Single(diagnostics, d => d.GetProperty("message").GetString()!.Contains("gone.png", StringComparison.Ordinal));
        Assert.Equal(["inner.svg"], nested.GetProperty("origin").EnumerateArray().Select(e => e.GetString()).ToList());
    }

    /// <summary>
    ///     Test that a plain note keeps its original shape, with no extra member.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_PlainNote_HasNoExtraMembers()
    {
        // Arrange: an SVG whose only link is not drawn
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(
            directory.Path,
            "diagram.svg",
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 40 40' width='40' height='40'>"
            + "<image href='missing.png' x='0' y='0' width='10' height='10'/></svg>");

        // Act
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke("render", input, "-o", directory.Combine("out.png"), "--json");

        // Assert: the note has exactly the members it always had
        Assert.True(exitCode == 0, stdErr + stdOut);
        using var document = JsonDocument.Parse(stdOut);
        var diagnostic = Assert.Single(document.RootElement.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal(
            ["source", "severity", "code", "message"],
            diagnostic.EnumerateObject().Select(p => p.Name).ToList());
    }

    /// <summary>
    ///     Test that <c>inspect</c> writes the same extra members for the notes of the file it reads.
    /// </summary>
    [Fact]
    public void InspectCommand_Run_ScadWarning_WritesDetailAndLocation()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "model.scad", ModelWithBadAnchor);

        // Act
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke("inspect", "colors", input, "--size", "32x32", "--json");

        // Assert
        Assert.True(exitCode == 0, stdErr + stdOut);
        using var document = JsonDocument.Parse(stdOut);
        var diagnostic = Assert.Single(document.RootElement.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal("scad-anchor-invalid", diagnostic.GetProperty("detail").GetString());
        Assert.Equal(2, diagnostic.GetProperty("location").GetProperty("line").GetInt32());
    }

    /// <summary>
    ///     Test that <c>compare</c> writes the extra members for the notes of either side, after the side label.
    /// </summary>
    [Fact]
    public void CompareCommand_Run_ScadWarning_WritesDetailAndLocation()
    {
        // Arrange: a model with a malformed anchor against a plain one
        using var directory = new TempDirectory();
        var a = FixtureFactory.WriteText(directory.Path, "a.scad", ModelWithBadAnchor);
        var b = FixtureFactory.WriteText(directory.Path, "b.scad", "cube(5);\n");

        // Act
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke("compare", a, b, "--json");

        // Assert
        Assert.True(exitCode == 0, stdErr + stdOut);
        using var document = JsonDocument.Parse(stdOut);
        var diagnostic = Assert.Single(document.RootElement.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal("scad-anchor-invalid", diagnostic.GetProperty("detail").GetString());
        Assert.Equal(2, diagnostic.GetProperty("location").GetProperty("line").GetInt32());
    }

    /// <summary>
    ///     Test that <c>montage</c> writes the extra members for the notes of a cell.
    /// </summary>
    [Fact]
    public void MontageCommand_Run_ScadWarning_WritesDetailAndLocation()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "model.scad", ModelWithBadAnchor);

        // Act
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke("montage", input, "--cell", "64x64", "-o", directory.Combine("out.png"), "--json");

        // Assert
        Assert.True(exitCode == 0, stdErr + stdOut);
        using var document = JsonDocument.Parse(stdOut);
        var diagnostic = Assert.Single(document.RootElement.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal("scad-anchor-invalid", diagnostic.GetProperty("detail").GetString());
        Assert.Equal(2, diagnostic.GetProperty("location").GetProperty("line").GetInt32());
    }
}
