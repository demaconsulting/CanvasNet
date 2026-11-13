// cspell:ignore scad svg href hrefs
using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Tests of the render command with an SVG file that embeds other files: the result document, the diagnostics, the
///     folder the files are confined to, strict mode, and the errors.
/// </summary>
[Collection("Sequential")]
public class RenderCommandSvgEmbedTests
{
    /// <summary>The red of the background of the SVG files of these tests.</summary>
    private static readonly Rgba32 Red = new(255, 0, 0, 255);

    /// <summary>The green of the picture that is embedded.</summary>
    private static readonly Rgba32 Green = new(0, 255, 0, 255);

    /// <summary>
    ///     Writes an SVG diagram whose canvas is 120 by 80 and red, with the given markup on top.
    /// </summary>
    /// <param name="directory">The folder to write into.</param>
    /// <param name="body">The markup drawn over the red background.</param>
    /// <returns>The path of the file.</returns>
    private static string WriteDiagram(string directory, string body)
    {
        return FixtureFactory.WriteText(
            directory,
            "diagram.svg",
            "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 120 80' width='120' height='80'>"
            + $"<rect width='120' height='80' fill='#ff0000'/>{body}</svg>");
    }

    /// <summary>
    ///     Writes a small solid green PNG file.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    private static void WriteGreenPng(string path)
    {
        using var surface = new Surface(4, 4);
        surface.Clear(Green);
        PngCodec.Save(surface, path);
    }

    /// <summary>
    ///     Test that a picture that is too large for the pixel budget (a file that declares 9000 by 9000 pixels, in a
    ///     20 by 20 placement) is a <c>svg-image-too-large</c> warning without <c>--strict</c>: the render succeeds, the
    ///     rest of the diagram is drawn, and nothing is read as a picture.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_SvgEmbeddingTooLargePicture_WarnsWithTooLargeCodeAndDrawsTheRest()
    {
        // Arrange: the huge picture at 20 by 20, and a normal one beside it
        using var directory = new TempDirectory();
        WriteGreenPng(directory.Combine("logo.png"));
        FixtureFactory.WriteDeclaredPng(directory.Combine("huge.png"), 9000, 9000);
        var input = WriteDiagram(
            directory.Path,
            "<image href='huge.png' x='10' y='10' width='20' height='20'/>"
            + "<image href='logo.png' x='90' y='50' width='20' height='20'/>");
        var output = directory.Combine("diagram.png");

        // Act
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke("render", input, "-o", output, "--json");

        // Assert: exit 0, the one warning, the normal picture drawn and the huge one not
        Assert.True(exitCode == 0, stdErr + stdOut);
        using var document = JsonDocument.Parse(stdOut);
        var diagnostic = Assert.Single(document.RootElement.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal("svg-image-too-large", diagnostic.GetProperty("code").GetString());
        Assert.Equal("warning", diagnostic.GetProperty("severity").GetString());
        Assert.Contains("'huge.png'", diagnostic.GetProperty("message").GetString(), StringComparison.Ordinal);
        using var image = PngCodec.Load(output);
        Assert.Equal(Red, image[20, 20]);
        Assert.Equal(Green, image[100, 60]);
    }

    /// <summary>
    ///     Test that with <c>--strict</c> a picture that is too large for the pixel budget fails with
    ///     <c>unsupported-feature</c> (like an unresolved reference), names the code of the warning in the hint, and
    ///     writes no output.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_SvgEmbeddingStrictTooLargePicture_FailsWithUnsupportedFeature()
    {
        // Arrange
        using var directory = new TempDirectory();
        FixtureFactory.WriteDeclaredPng(directory.Combine("huge.png"), 9000, 9000);
        var input = WriteDiagram(directory.Path, "<image href='huge.png' x='10' y='10' width='20' height='20'/>");
        var output = directory.Combine("diagram.png");

        // Act
        var (exitCode, stdOut, _) = ToolHost.Invoke("render", input, "--strict", "-o", output, "--json");

        // Assert
        Assert.Equal(1, exitCode);
        using var document = JsonDocument.Parse(stdOut);
        var error = document.RootElement.GetProperty("error");
        Assert.Equal(ErrorCodes.UnsupportedFeature, error.GetProperty("code").GetString());
        Assert.Contains("huge.png", error.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Contains("svg-image-too-large", error.GetProperty("hint").GetString(), StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    /// <summary>
    ///     Test that a link with a query names the file before it: the picture is drawn, without diagnostics, also with
    ///     <c>--strict</c>, and the result lists the link as written.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_SvgEmbeddingHrefWithQuery_DrawsTheFile()
    {
        // Arrange
        using var directory = new TempDirectory();
        WriteGreenPng(directory.Combine("logo.png"));
        var input = WriteDiagram(directory.Path, "<image href='logo.png?v=2' x='90' y='50' width='20' height='20'/>");
        var output = directory.Combine("diagram.png");

        // Act
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke("render", input, "--strict", "-o", output, "--json");

        // Assert
        Assert.True(exitCode == 0, stdErr + stdOut);
        using var document = JsonDocument.Parse(stdOut);
        Assert.Empty(document.RootElement.GetProperty("diagnostics").EnumerateArray());
        var embed = Assert.Single(document.RootElement.GetProperty("operations").EnumerateArray(), o => o.GetProperty("op").GetString() == "embed");
        Assert.Equal("logo.png?v=2", embed.GetProperty("href").GetString());
        using var image = PngCodec.Load(output);
        Assert.Equal(Green, image[100, 60]);
    }

    /// <summary>
    ///     Test that a link with a fragment chooses the page of a document (<c>report.pdf#page=2</c>, <c>deck.pptx#slide=2</c>,
    ///     <c>drawing.vsdx#page=2</c>), without diagnostics, also with <c>--strict</c>, and the result lists the links as written.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_SvgEmbeddingPageFragments_DrawsTheChosenPages()
    {
        // Arrange: the second page of a PDF, the second slide of a presentation and the second page of a drawing
        using var directory = new TempDirectory();
        File.WriteAllBytes(directory.Combine("report.pdf"), FixtureFactory.CreatePdf());
        File.WriteAllBytes(directory.Combine("deck.pptx"), FixtureFactory.CreatePptx());
        File.WriteAllBytes(directory.Combine("drawing.vsdx"), FixtureFactory.CreateVsdx());
        var input = WriteDiagram(
            directory.Path,
            "<image href='report.pdf#page=2' x='0' y='0' width='40' height='40'/>"
            + "<image href='deck.pptx#slide=2' x='40' y='0' width='40' height='40'/>"
            + "<image href='drawing.vsdx#page=2' x='80' y='0' width='40' height='40'/>");
        var output = directory.Combine("diagram.png");

        // Act
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke("render", input, "--strict", "-o", output, "--json");

        // Assert
        Assert.True(exitCode == 0, stdErr + stdOut);
        using var document = JsonDocument.Parse(stdOut);
        Assert.Empty(document.RootElement.GetProperty("diagnostics").EnumerateArray());
        var hrefs = document.RootElement.GetProperty("operations").EnumerateArray()
            .Where(o => o.GetProperty("op").GetString() == "embed")
            .Select(o => o.GetProperty("href").GetString())
            .ToList();
        Assert.Equal(["report.pdf#page=2", "deck.pptx#slide=2", "drawing.vsdx#page=2"], hrefs);
        using var image = PngCodec.Load(output);
        Assert.Equal(new Rgba32(0, 153, 0, 255), image[20, 20]);
        Assert.Equal(new Rgba32(0, 0, 255, 255), image[60, 12]);
        Assert.Equal(new Rgba32(0, 0, 255, 255), image[100, 30]);
    }

    /// <summary>
    ///     Test that a page that does not exist is one unsupported note with the page-out-of-range code and the embedding
    ///     file as origin, exit code 0 without <c>--strict</c>, and the rest of the diagram is drawn.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_SvgEmbeddingPageBeyondTheLast_IsUnsupportedNoteAndDrawsTheRest()
    {
        // Arrange
        using var directory = new TempDirectory();
        File.WriteAllBytes(directory.Combine("report.pdf"), FixtureFactory.CreatePdf());
        WriteGreenPng(directory.Combine("logo.png"));
        var input = WriteDiagram(
            directory.Path,
            "<image href='report.pdf#page=9' x='10' y='10' width='20' height='20'/>"
            + "<image href='logo.png' x='90' y='50' width='20' height='20'/>");
        var output = directory.Combine("diagram.png");

        // Act
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke("render", input, "-o", output, "--json");

        // Assert
        Assert.True(exitCode == 0, stdErr + stdOut);
        using var document = JsonDocument.Parse(stdOut);
        var diagnostic = Assert.Single(document.RootElement.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal("image-page-out-of-range", diagnostic.GetProperty("code").GetString());
        Assert.Equal("unsupported", diagnostic.GetProperty("severity").GetString());
        Assert.Contains("page 9 does not exist", diagnostic.GetProperty("message").GetString(), StringComparison.Ordinal);
        using var image = PngCodec.Load(output);
        Assert.Equal(Red, image[20, 20]);
        Assert.Equal(Green, image[100, 60]);
    }

    /// <summary>
    ///     Test that with <c>--strict</c> a page that does not exist, or a fragment that cannot be used, fails with
    ///     <c>unsupported-feature</c> naming the link and the code, and writes no output.
    /// </summary>
    /// <param name="fragment">The fragment of the link.</param>
    /// <param name="code">The code the hint names.</param>
    [Theory]
    [InlineData("page=9", "image-page-out-of-range")]
    [InlineData("page=zero", "image-fragment-invalid")]
    public void RenderCommand_Run_SvgEmbeddingStrictPageProblem_FailsWithUnsupportedFeature(string fragment, string code)
    {
        // Arrange
        using var directory = new TempDirectory();
        File.WriteAllBytes(directory.Combine("report.pdf"), FixtureFactory.CreatePdf());
        var input = WriteDiagram(directory.Path, $"<image href='report.pdf#{fragment}' x='10' y='10' width='20' height='20'/>");
        var output = directory.Combine("diagram.png");

        // Act
        var (exitCode, stdOut, _) = ToolHost.Invoke("render", input, "--strict", "-o", output, "--json");

        // Assert
        Assert.Equal(1, exitCode);
        using var document = JsonDocument.Parse(stdOut);
        var error = document.RootElement.GetProperty("error");
        Assert.Equal(ErrorCodes.UnsupportedFeature, error.GetProperty("code").GetString());
        Assert.Contains("report.pdf#" + fragment, error.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Contains(code, error.GetProperty("hint").GetString(), StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    /// <summary>
    ///     Test that a fragment parameter that the source does not use, or a parameter given twice with different values,
    ///     is a warning only: the picture is drawn, and <c>--strict</c> still succeeds.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_SvgEmbeddingUnknownAndDuplicateParameters_AreWarningsEvenWhenStrict()
    {
        // Arrange
        using var directory = new TempDirectory();
        WriteGreenPng(directory.Combine("logo.png"));
        File.WriteAllBytes(directory.Combine("report.pdf"), FixtureFactory.CreatePdf());
        var input = WriteDiagram(
            directory.Path,
            "<image href='logo.png#top' x='90' y='50' width='20' height='20'/>"
            + "<image href='report.pdf#page=1&amp;page=2' x='10' y='10' width='20' height='20'/>");
        var output = directory.Combine("diagram.png");

        // Act
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke("render", input, "--strict", "-o", output, "--json");

        // Assert
        Assert.True(exitCode == 0, stdErr + stdOut);
        using var document = JsonDocument.Parse(stdOut);
        var codes = document.RootElement.GetProperty("diagnostics").EnumerateArray().Select(d => d.GetProperty("code").GetString()).ToList();
        Assert.Equal(["image-fragment-unknown-parameter", "image-fragment-duplicate-parameter"], codes);
        Assert.All(
            document.RootElement.GetProperty("diagnostics").EnumerateArray(),
            d => Assert.Equal("warning", d.GetProperty("severity").GetString()));
        using var image = PngCodec.Load(output);
        Assert.Equal(Green, image[100, 60]);
    }

    /// <summary>
    ///     Test that a diagram embedding a model, a chart, and a picture is rendered, that the result lists each
    ///     embedded file, and that nothing is reported.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_SvgEmbeddingModelChartAndPicture_DrawsAllAndListsEmbeds()
    {
        // Arrange: a diagram with one placement for each kind of file, all beside it
        using var directory = new TempDirectory();
        File.Copy(FixtureFactory.FixturePath("cube.scad"), directory.Combine("part.scad"));
        File.Copy(FixtureFactory.FixturePath("baskets.vl.json"), directory.Combine("baskets.vl.json"));
        WriteGreenPng(directory.Combine("logo.png"));
        var input = WriteDiagram(
            directory.Path,
            "<image href='part.scad' x='0' y='0' width='40' height='40'/>"
            + "<image href='baskets.vl.json' x='40' y='0' width='40' height='40'/>"
            + "<image href='logo.png' x='90' y='50' width='20' height='20'/>");
        var output = directory.Combine("diagram.png");

        // Act
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke("render", input, "-o", output, "--json");

        // Assert: success without notes, the three embeds in order, and the pixels of each placement
        Assert.True(exitCode == 0, stdErr + stdOut);
        using var document = JsonDocument.Parse(stdOut);
        var root = document.RootElement;
        Assert.True(root.GetProperty("ok").GetBoolean());
        Assert.Empty(root.GetProperty("diagnostics").EnumerateArray());
        var embeds = root.GetProperty("operations").EnumerateArray().Where(o => o.GetProperty("op").GetString() == "embed").ToList();
        Assert.Equal(
            [("part.scad", "scad"), ("baskets.vl.json", "vega-lite"), ("logo.png", "png")],
            embeds.Select(e => (e.GetProperty("href").GetString()!, e.GetProperty("format").GetString()!)));
        using var image = PngCodec.Load(output);
        Assert.Equal(new Rgba32(255, 255, 255, 255), image[41, 1]);
        Assert.Equal(Green, image[100, 60]);
        Assert.Equal(Red, image[60, 70]);
    }

    /// <summary>
    ///     Test that, under the default file-access policy, a reference to a file outside the folder of the diagram is
    ///     drawn like Pandoc draws it, with no diagnostic.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_SvgEmbeddingOutsideFolder_IsDrawnUnderTheDefaultPolicy()
    {
        // Arrange: a valid picture above the folder of the diagram, referenced by a path that leaves the folder
        using var directory = new TempDirectory();
        Directory.CreateDirectory(directory.Combine("site"));
        WriteGreenPng(directory.Combine("secret.png"));
        var input = WriteDiagram(directory.Combine("site"), "<image href='../secret.png' x='10' y='10' width='20' height='20'/>");
        var output = directory.Combine("diagram.png");

        // Act
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke("render", input, "-o", output, "--json");

        // Assert: exit 0, no diagnostic, and the picture of the file is in the output
        Assert.Equal(0, exitCode);
        using var document = JsonDocument.Parse(stdOut);
        Assert.Empty(document.RootElement.GetProperty("diagnostics").EnumerateArray());
        using var image = PngCodec.Load(output);
        Assert.Equal(Green, image[20, 20]);
        Assert.Equal(string.Empty, stdErr);
    }

    /// <summary>
    ///     Test that without <c>--json</c> the warning is written to standard error as one line.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_SvgEmbeddingMissingFile_WritesWarningLineToStandardError()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = WriteDiagram(directory.Path, "<image href='missing.png' x='10' y='10' width='20' height='20'/>");

        // Act
        var (exitCode, _, stdErr) = ToolHost.Invoke("render", input, "-o", directory.Combine("diagram.png"));

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains("Warning: svg-image-unresolved: image 'missing.png' was not drawn", stdErr, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that with <c>--strict</c> an unresolved reference fails with <c>unsupported-feature</c> and writes no
    ///     output.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_SvgEmbeddingStrictUnresolved_FailsWithUnsupportedFeature()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = WriteDiagram(directory.Path, "<image href='https://example.invalid/x.png' x='10' y='10' width='20' height='20'/>");
        var output = directory.Combine("diagram.png");

        // Act
        var (exitCode, stdOut, _) = ToolHost.Invoke("render", input, "--strict", "-o", output, "--json");

        // Assert: the failure document carries the code and the file was not written
        Assert.Equal(1, exitCode);
        using var document = JsonDocument.Parse(stdOut);
        Assert.Equal(ErrorCodes.UnsupportedFeature, document.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.False(File.Exists(output));
    }

    /// <summary>
    ///     Test that an embedded model with an unsupported feature is a note without <c>--strict</c> and an error with it.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_SvgEmbeddingModelWithUnsupportedFeature_IsNoteThenStrictError()
    {
        // Arrange
        using var directory = new TempDirectory();
        FixtureFactory.WriteText(directory.Path, "part.scad", "offset(r = 1) square(2);\ncube(5);\n");
        var input = WriteDiagram(directory.Path, "<image href='part.scad' x='0' y='0' width='40' height='40'/>");

        // Act
        var (plainExit, plainOut, _) = ToolHost.Invoke("render", input, "-o", directory.Combine("plain.png"), "--json");
        var (strictExit, strictOut, _) = ToolHost.Invoke("render", input, "--strict", "-o", directory.Combine("strict.png"), "--json");

        // Assert
        Assert.Equal(0, plainExit);
        using var plain = JsonDocument.Parse(plainOut);
        var note = Assert.Single(plain.RootElement.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal("scad-module-offset", note.GetProperty("code").GetString());
        Assert.StartsWith("part.scad: ", note.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Equal(1, strictExit);
        using var strict = JsonDocument.Parse(strictOut);
        Assert.Equal(ErrorCodes.UnsupportedFeature, strict.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.False(File.Exists(directory.Combine("strict.png")));
    }

    /// <summary>
    ///     Test that a broken embedded model fails the render with its own code, names both files, and writes no output.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_SvgEmbeddingBrokenModel_FailsWithScadErrorNamingBothFiles()
    {
        // Arrange
        using var directory = new TempDirectory();
        FixtureFactory.WriteText(directory.Path, "broken.scad", "cube(;");
        var input = WriteDiagram(directory.Path, "<image href='broken.scad' x='0' y='0' width='40' height='40'/>");
        var output = directory.Combine("diagram.png");

        // Act
        var (exitCode, stdOut, _) = ToolHost.Invoke("render", input, "-o", output, "--json");

        // Assert
        Assert.Equal(1, exitCode);
        using var document = JsonDocument.Parse(stdOut);
        var error = document.RootElement.GetProperty("error");
        Assert.Equal(ErrorCodes.ScadError, error.GetProperty("code").GetString());
        var message = error.GetProperty("message").GetString()!;
        Assert.Contains("diagram.svg", message, StringComparison.Ordinal);
        Assert.Contains("broken.scad", message, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    /// <summary>
    ///     Test that an SVG file without a reference to another file is rendered as before: no diagnostics and no embed
    ///     operation, also with <c>--strict</c>.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_SvgWithoutEmbeddedFiles_IsUnchangedAndStrictSafe()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteSource(directory.Path, "svg");

        // Act
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke("render", input, "--strict", "-o", directory.Combine("out.png"), "--json");

        // Assert
        Assert.True(exitCode == 0, stdErr + stdOut);
        using var document = JsonDocument.Parse(stdOut);
        Assert.Empty(document.RootElement.GetProperty("diagnostics").EnumerateArray());
        Assert.DoesNotContain(
            document.RootElement.GetProperty("operations").EnumerateArray(),
            o => o.GetProperty("op").GetString() == "embed");
    }
}
