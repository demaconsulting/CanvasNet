using System.Text.Json;
using DemaConsulting.CanvasNet.Tool.Commands;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Unit tests for the FlattenCommand class: the options, the text and JSON results, and the errors.
/// </summary>
[Collection("Sequential")]
public class FlattenCommandTests
{
    /// <summary>
    ///     Runs the flatten command and captures what it writes.
    /// </summary>
    /// <param name="json"><see langword="true"/> to run with the global <c>--json</c> option.</param>
    /// <param name="args">The command arguments.</param>
    /// <returns>The captured standard output and standard error.</returns>
    private static (string StdOut, string StdErr) Run(bool json, params string[] args)
    {
        using var context = Context.Create(json ? ["--json"] : []);
        var command = new FlattenCommand();
        return ToolHost.Capture(() => command.Run(context, args));
    }

    /// <summary>
    ///     Runs the flatten command and returns the error it fails with.
    /// </summary>
    /// <param name="args">The command arguments.</param>
    /// <returns>The exception.</returns>
    private static ToolException RunFailing(params string[] args)
    {
        using var context = Context.Create([]);
        var command = new FlattenCommand();
        var (exception, _, _) = ToolHost.Capture(() => ToolHost.ExpectFailure(() => command.Run(context, args)));
        return exception;
    }

    /// <summary>
    ///     Writes a drawing of 100 by 100 pixels with the given markup, and the Mermaid diagram of the fixtures beside it.
    /// </summary>
    /// <param name="directory">The folder.</param>
    /// <param name="body">The markup inside the root element.</param>
    /// <returns>The path of the drawing.</returns>
    internal static string WriteDrawing(TempDirectory directory, string body)
    {
        File.Copy(FixtureFactory.FixturePath("mermaid/flow.mmd"), directory.Combine("flow.mmd"), true);
        return FixtureFactory.WriteText(
            directory.Path,
            "dashboard.svg",
            $"<svg xmlns='http://www.w3.org/2000/svg' width='100' height='100'><rect width='100' height='100' fill='#eeeeee'/>{body}</svg>");
    }

    /// <summary>
    ///     Test that the command declares its name, usage, and options.
    /// </summary>
    [Fact]
    public void FlattenCommand_Help_ShowsUsageAndOptions()
    {
        // Arrange / Act
        var command = new FlattenCommand();

        // Assert
        Assert.Equal("flatten", command.Name);
        Assert.Empty(command.Aliases);
        Assert.False(string.IsNullOrWhiteSpace(command.Summary));
        Assert.StartsWith("canvasnet flatten <input.svg> -o <output.svg>", command.Usage, StringComparison.Ordinal);
        Assert.Contains("Examples:", command.Usage, StringComparison.Ordinal);
        Assert.Equal(["--output", "--scale", "--strict", "--no-clobber"], command.Options.Select(o => o.Name));
        Assert.Equal(["-o"], command.Options.Single(o => o.Name == "--output").Aliases);
    }

    /// <summary>
    ///     Test that a drawing that links a diagram is flattened and the text names what was replaced.
    /// </summary>
    [Fact]
    public void FlattenCommand_Run_CompositeSvg_WritesCopyAndPrintsSummary()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = WriteDrawing(directory, "<image href='flow.mmd' width='60' height='40'/>");
        var output = directory.Combine("flat.svg");

        // Act
        var (stdOut, stdErr) = Run(false, input, "-o", output);

        // Assert
        Assert.Equal(
            $"wrote {output} ({new FileInfo(output).Length} bytes): 1 picture replaced (flow.mmd 60x40 mermaid){Environment.NewLine}",
            stdOut);
        Assert.Empty(stdErr);
        Assert.Contains("data:image/png;base64,", File.ReadAllText(output), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the JSON document has the documented keys in the documented order.
    /// </summary>
    [Fact]
    public void FlattenCommand_Run_Json_HasDocumentedShapeAndKeyOrder()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = WriteDrawing(directory, "<image href='flow.mmd' width='60' height='40'/>");
        var output = directory.Combine("flat.svg");

        // Act
        var (stdOut, _) = Run(true, input, "-o", output);

        // Assert
        using var document = JsonDocument.Parse(stdOut);
        var root = document.RootElement;
        Assert.Equal(
            ["schemaVersion", "ok", "command", "tool", "input", "scale", "flattened", "replaced", "rebased", "outputs", "diagnostics", "echo"],
            root.EnumerateObject().Select(p => p.Name));
        Assert.Equal("flatten", root.GetProperty("command").GetString());
        Assert.Equal("svg", root.GetProperty("input").GetProperty("format").GetString());
        Assert.Equal(100, root.GetProperty("input").GetProperty("natural").GetProperty("width").GetInt32());
        Assert.True(root.GetProperty("flattened").GetBoolean());
        var replaced = Assert.Single(root.GetProperty("replaced").EnumerateArray());
        Assert.Equal(["href", "format", "width", "height", "elements", "bytes"], replaced.EnumerateObject().Select(p => p.Name));
        Assert.Equal("flow.mmd", replaced.GetProperty("href").GetString());
        var written = Assert.Single(root.GetProperty("outputs").EnumerateArray());
        Assert.Equal(output, written.GetProperty("path").GetString());
        Assert.Equal(new FileInfo(output).Length, written.GetProperty("bytes").GetInt64());
    }

    /// <summary>
    ///     Test that a drawing with nothing to flatten says so and writes nothing.
    /// </summary>
    [Fact]
    public void FlattenCommand_Run_PlainSvg_PrintsNothingToFlattenAndWritesNothing()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = WriteDrawing(directory, string.Empty);
        var output = directory.Combine("flat.svg");

        // Act
        var (stdOut, _) = Run(false, input, "-o", output);

        // Assert
        Assert.Equal(
            $"nothing to flatten: {input} links no picture that only CanvasNet draws; {output} was not written{Environment.NewLine}",
            stdOut);
        Assert.False(File.Exists(output));
    }

    /// <summary>
    ///     Test that the JSON document of a drawing with nothing to flatten has flattened false and no outputs.
    /// </summary>
    [Fact]
    public void FlattenCommand_Run_Json_PlainSvg_FlattenedFalse()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = WriteDrawing(directory, string.Empty);

        // Act
        var (stdOut, _) = Run(true, input, "-o", directory.Combine("flat.svg"));

        // Assert
        using var document = JsonDocument.Parse(stdOut);
        var root = document.RootElement;
        Assert.False(root.GetProperty("flattened").GetBoolean());
        Assert.Equal(0, root.GetProperty("replaced").GetArrayLength());
        Assert.Equal(0, root.GetProperty("outputs").GetArrayLength());
    }

    /// <summary>
    ///     Test that a note about a page of a linked document carries its page, as the notes of render do.
    /// </summary>
    [Fact]
    public void FlattenCommand_Run_Json_LinkedDocumentNote_HasPage()
    {
        // Arrange: a drawing that links a presentation whose first slide has a picture the renderer skips
        using var directory = new TempDirectory();
        File.WriteAllBytes(directory.Combine("deck.pptx"), FixtureFactory.CreatePptx(withMetafile: true));
        var input = WriteDrawing(directory, "<image href='deck.pptx' width='60' height='40'/>");

        // Act
        var (stdOut, _) = Run(true, input, "-o", directory.Combine("flat.svg"));

        // Assert: the note has its page right after the message
        using var document = JsonDocument.Parse(stdOut);
        var note = Assert.Single(
            document.RootElement.GetProperty("diagnostics").EnumerateArray(),
            n => n.GetProperty("source").GetString() == "pptx");
        Assert.Equal(["source", "severity", "code", "message", "page"], note.EnumerateObject().Select(p => p.Name).Take(5));
        Assert.Equal(1, note.GetProperty("page").GetInt32());
    }

    /// <summary>
    ///     Test that <c>--scale</c> multiplies the size of the replaced picture.
    /// </summary>
    [Fact]
    public void FlattenCommand_Run_Scale_DoublesReplacedSize()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = WriteDrawing(directory, "<image href='flow.mmd' width='60' height='40'/>");
        var output = directory.Combine("flat.svg");

        // Act
        var (stdOut, _) = Run(false, input, "-o", output, "--scale", "2");

        // Assert
        Assert.Contains("(flow.mmd 120x80 mermaid)", stdOut, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a link that is not drawn fails the command with <c>--strict</c>.
    /// </summary>
    [Fact]
    public void FlattenCommand_Run_Strict_UnresolvedLink_FailsUnsupportedFeature()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = WriteDrawing(directory, "<image href='missing.scad' width='60' height='40'/>");

        // Act
        var ex = RunFailing(input, "-o", directory.Combine("flat.svg"), "--strict");

        // Assert
        Assert.Equal(ErrorCodes.UnsupportedFeature, ex.Code);
    }

    /// <summary>
    ///     Test that a diagram the drawing never shows is a warning on standard error.
    /// </summary>
    [Fact]
    public void FlattenCommand_Run_HiddenDiagram_WarnsOnStandardError()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = WriteDrawing(directory, "<defs><image id='x' href='flow.mmd' width='60' height='40'/></defs>");

        // Act
        var (stdOut, stdErr) = Run(false, input, "-o", directory.Combine("flat.svg"));

        // Assert
        Assert.StartsWith("nothing to flatten", stdOut, StringComparison.Ordinal);
        Assert.Contains($"Warning: {SvgFlattener.ImageNotDrawnCode}: image 'flow.mmd'", stdErr, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a missing output is a usage error.
    /// </summary>
    [Fact]
    public void FlattenCommand_Run_NoOutput_UsageError()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = WriteDrawing(directory, string.Empty);

        // Act
        var ex = RunFailing(input);

        // Assert
        Assert.Equal(ErrorCodes.UsageError, ex.Code);
        Assert.Contains("-o", ex.Hint, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that two inputs are a usage error.
    /// </summary>
    [Fact]
    public void FlattenCommand_Run_TwoInputs_UsageError()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = WriteDrawing(directory, string.Empty);

        // Act
        var ex = RunFailing(input, input, "-o", directory.Combine("flat.svg"));

        // Assert
        Assert.Equal(ErrorCodes.UsageError, ex.Code);
    }

    /// <summary>
    ///     Test that an input that is not an SVG drawing is refused.
    /// </summary>
    [Fact]
    public void FlattenCommand_Run_NotSvg_UnsupportedFormat()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = CompareCommandTests.SaveSolid(directory, "a.png", 4, 4, MontageCommandTests.Red);

        // Act
        var ex = RunFailing(input, "-o", directory.Combine("flat.svg"));

        // Assert
        Assert.Equal(ErrorCodes.UnsupportedFormat, ex.Code);
    }

    /// <summary>
    ///     Test that an existing output is refused with <c>--no-clobber</c>.
    /// </summary>
    [Fact]
    public void FlattenCommand_Run_NoClobber_OutputExists()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = WriteDrawing(directory, "<image href='flow.mmd' width='60' height='40'/>");
        var output = FixtureFactory.WriteText(directory.Path, "flat.svg", "keep");

        // Act
        var ex = RunFailing(input, "-o", output, "--no-clobber");

        // Assert
        Assert.Equal(ErrorCodes.OutputExists, ex.Code);
        Assert.Equal("keep", File.ReadAllText(output));
    }
}
