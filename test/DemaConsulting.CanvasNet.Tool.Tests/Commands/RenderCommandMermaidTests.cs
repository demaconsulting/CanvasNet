using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Tool.Commands;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Tests of the render command (and <c>inspect</c>) with Mermaid diagrams: the picture and its operation, the
///     <c>--theme</c> option and its errors, the theme fragment of an input, the notes and <c>--strict</c>, and the errors of
///     the library.
/// </summary>
[Collection("Sequential")]
public class RenderCommandMermaidTests
{
    /// <summary>A small flowchart of two nodes.</summary>
    private const string Flow = "flowchart LR\n    a[Start] --> b[Stop]\n";

    /// <summary>The fill of a node in Mermaid's default theme.</summary>
    private static readonly Rgba32 DefaultNodeFill = new(0xEC, 0xEC, 0xFF, 255);

    /// <summary>The fill of a node in Mermaid's forest theme (<c>mainBkg</c>).</summary>
    private static readonly Rgba32 ForestNodeFill = new(0xCD, 0xE4, 0x98, 255);

    /// <summary>
    ///     Runs the render command with <c>--json</c> and returns the document it prints.
    /// </summary>
    /// <param name="args">The command arguments.</param>
    /// <returns>The parsed document; the caller disposes it.</returns>
    private static JsonDocument RunJson(params string[] args)
    {
        using var context = Context.Create(["--json"]);
        var command = new RenderCommand();
        var (stdOut, _) = ToolHost.Capture(() => command.Run(context, args));
        return JsonDocument.Parse(stdOut);
    }

    /// <summary>
    ///     Runs the render command and returns the error it fails with.
    /// </summary>
    /// <param name="args">The command arguments.</param>
    /// <returns>The exception.</returns>
    private static ToolException RunFailing(params string[] args)
    {
        using var context = Context.Create([]);
        var command = new RenderCommand();
        return ToolHost.ExpectFailure(() => command.Run(context, args));
    }

    /// <summary>
    ///     Counts the pixels of a picture that have a color.
    /// </summary>
    /// <param name="surface">The picture.</param>
    /// <param name="color">The color.</param>
    /// <returns>The number of pixels.</returns>
    private static int Count(Surface surface, Rgba32 color) =>
        Enumerable.Range(0, surface.Height).Sum(y => Enumerable.Range(0, surface.Width).Count(x => surface[x, y] == color));

    /// <summary>
    ///     Test that a diagram is drawn at its natural size over white, and that the result names the format, the natural size,
    ///     and the render operation.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_Mermaid_WritesTheDiagramAndDescribesIt()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "flow.mmd", Flow);
        var output = directory.Combine("flow.png");

        // Act
        using var document = RunJson(input, "-o", output, "--strict");

        // Assert
        var root = document.RootElement;
        Assert.Equal("mermaid", root.GetProperty("input").GetProperty("format").GetString());
        var natural = root.GetProperty("input").GetProperty("natural");
        using var image = PngCodec.Load(output);
        Assert.Equal((natural.GetProperty("width").GetInt32(), natural.GetProperty("height").GetInt32()), (image.Width, image.Height));
        Assert.Equal(new Rgba32(255, 255, 255, 255), image[0, 0]);
        Assert.True(Count(image, DefaultNodeFill) > 500);
        var render = root.GetProperty("operations")[0];
        Assert.Equal("render", render.GetProperty("op").GetString());
        Assert.Equal("#FFFFFF", render.GetProperty("background").GetString());
        Assert.False(render.TryGetProperty("theme", out _));
        Assert.Empty(root.GetProperty("diagnostics").EnumerateArray());
    }

    /// <summary>
    ///     Test that <c>--theme</c> draws the diagram in that theme (without regard to the case of the name) and records it, that
    ///     the theme of a fragment of the input wins over the option, and that a theme the text sets wins over both.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_MermaidTheme_OptionFragmentAndTextPrecedence()
    {
        // Arrange: the flowchart, and the same flowchart with its own theme in the frontmatter
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "flow.mmd", Flow);
        var own = FixtureFactory.WriteText(directory.Path, "own.mmd", "---\nconfig:\n  theme: forest\n---\n" + Flow);

        // Act
        using var option = RunJson(input, "--theme", "Forest", "-o", directory.Combine("option.png"));
        using var fragment = RunJson(input + "#theme=forest", "--theme", "dark", "-o", directory.Combine("fragment.png"));
        using var text = RunJson(own, "--theme", "dark", "-o", directory.Combine("text.png"));

        // Assert: the forest fill in all three pictures, and the theme in the operation of the first two
        foreach (var name in new[] { "option.png", "fragment.png", "text.png" })
        {
            using var image = PngCodec.Load(directory.Combine(name));
            Assert.True(Count(image, ForestNodeFill) > 500, $"{name} is not drawn in the forest theme.");
        }

        Assert.Equal("forest", option.RootElement.GetProperty("operations")[0].GetProperty("theme").GetString());
        Assert.Equal("forest", fragment.RootElement.GetProperty("operations")[0].GetProperty("theme").GetString());
        Assert.Equal("dark", text.RootElement.GetProperty("operations")[0].GetProperty("theme").GetString());
    }

    /// <summary>
    ///     Test that <c>--theme</c> with a name that is not a Mermaid theme is a usage error that lists the themes, and that
    ///     <c>--theme</c> on an input that is not a diagram is a usage error for render and inspect, before anything is drawn.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_MermaidThemeMistakes_AreUsageErrors()
    {
        // Arrange
        using var directory = new TempDirectory();
        var diagram = FixtureFactory.WriteText(directory.Path, "flow.mmd", Flow);
        var model = FixtureFactory.WriteSource(directory.Path, "scad");

        // Act
        var unknown = RunFailing(diagram, "--theme", "sunset", "-o", directory.Combine("a.png"));
        var notDiagram = RunFailing(model, "--theme", "dark", "-o", directory.Combine("b.png"));
        var (inspectExit, _, inspectErr) = ToolHost.Invoke("inspect", "colors", model, "--theme", "dark");

        // Assert
        Assert.Equal(ErrorCodes.UsageError, unknown.Code);
        Assert.Contains("'sunset' is not a Mermaid theme", unknown.Message, StringComparison.Ordinal);
        Assert.Contains("default, base, dark, forest, neutral", unknown.Hint, StringComparison.Ordinal);
        Assert.Equal(ErrorCodes.UsageError, notDiagram.Code);
        Assert.Contains("--theme only applies to a Mermaid diagram", notDiagram.Message, StringComparison.Ordinal);
        Assert.Contains("scad file", notDiagram.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(directory.Combine("b.png")));
        Assert.Equal(1, inspectExit);
        Assert.Contains("Error: usage-error: The option --theme only applies to a Mermaid diagram", inspectErr, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a diagram type this version does not draw renders an empty picture with the diagnostic
    ///     <c>mermaid-diagram-unsupported</c>, which says that the type is planned, and fails with <c>--strict</c>.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_MermaidSequence_IsUnsupportedDiagnosticThenStrictError()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "talk.mmd", "sequenceDiagram\n    Alice->>Bob: Hello\n");

        // Act
        using var document = RunJson(input, "-o", directory.Combine("talk.png"));
        var strict = RunFailing(input, "--strict", "-o", directory.Combine("strict.png"));

        // Assert: one diagnostic with its source, severity, code, message and location
        var diagnostic = Assert.Single(document.RootElement.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal("mermaid", diagnostic.GetProperty("source").GetString());
        Assert.Equal("unsupported", diagnostic.GetProperty("severity").GetString());
        Assert.Equal("mermaid-diagram-unsupported", diagnostic.GetProperty("code").GetString());
        Assert.Contains("sequence diagrams are planned", diagnostic.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Equal(1, diagnostic.GetProperty("location").GetProperty("line").GetInt32());
        Assert.Equal(ErrorCodes.UnsupportedFeature, strict.Code);
        Assert.False(File.Exists(directory.Combine("strict.png")));
    }

    /// <summary>
    ///     Test that a warning of the library is the diagnostic <c>mermaid-warning</c> with the specific code in <c>detail</c>
    ///     and the position in <c>location</c>, and that invalid text is a <c>mermaid-error</c> with the hint of the library.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_MermaidWarningAndSyntaxError_FollowTheContract()
    {
        // Arrange
        using var directory = new TempDirectory();
        var noisy = FixtureFactory.WriteText(directory.Path, "noisy.mmd", Flow + "    style b fill:#f00,text-decoration:underline\n");
        var broken = FixtureFactory.WriteText(directory.Path, "broken.mmd", "flowchart LR\n    a[open --> b\n");

        // Act
        using var document = RunJson(noisy, "-o", directory.Combine("noisy.png"));
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke("render", broken, "-o", directory.Combine("broken.png"), "--json");

        // Assert: the warning
        var warning = Assert.Single(document.RootElement.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal("mermaid-warning", warning.GetProperty("code").GetString());
        Assert.Equal("mermaid-style-ignored", warning.GetProperty("detail").GetString());
        Assert.True(warning.GetProperty("location").GetProperty("line").GetInt32() > 1);

        // Assert: the error, with a hint, and no output
        Assert.Equal(1, exitCode);
        Assert.Contains("Error: mermaid-error: Mermaid syntax error:", stdErr, StringComparison.Ordinal);
        Assert.Contains("Hint: ", stdErr, StringComparison.Ordinal);
        using var failure = JsonDocument.Parse(stdOut);
        Assert.Equal("mermaid-error", failure.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.False(File.Exists(directory.Combine("broken.png")));
    }

    /// <summary>
    ///     Test that inspect reads a diagram with its theme like render does.
    /// </summary>
    [Fact]
    public void InspectCommand_Run_MermaidTheme_ReportsTheThemedColors()
    {
        // Arrange: a point a few pixels inside the first node, which starts at the diagram padding of 8 pixels
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "flow.mmd", Flow);
        var point = $"14,{SourceLoader.Probe(input).Height / 2}";

        // Act
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke("inspect", "pixel", input, point, "--theme", "forest", "--json");
        var (plainExit, plainOut, _) = ToolHost.Invoke("inspect", "pixel", input, point, "--json");

        // Assert: the forest fill with the option, and the default fill without it
        Assert.True(exitCode == 0, stdErr + stdOut);
        Assert.Contains("#CDE498", stdOut, StringComparison.Ordinal);
        Assert.Equal(0, plainExit);
        Assert.Contains("#ECECFF", plainOut, StringComparison.Ordinal);
    }
}
