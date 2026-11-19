using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Tool.Commands;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Tests of the render command (and <c>inspect</c>) with PlantUML diagrams: the picture and its operation, the options
///     that only a Mermaid diagram takes, the notes and <c>--strict</c>, and the errors of the library.
/// </summary>
[Collection("Sequential")]
public class RenderCommandPlantUmlTests
{
    /// <summary>A small sequence diagram of two participants and two messages.</summary>
    private const string Sequence = "@startuml\ntitle Talk\nbox Office\nparticipant Alice\nend box\nAlice -> Bob : Hello\nBob --> Alice : Hi\n@enduml\n";

    /// <summary>The fill of a participant in PlantUML's default look.</summary>
    private static readonly Rgba32 ParticipantFill = new(0xE2, 0xE2, 0xF0, 255);

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
    ///     Test that a sequence diagram is drawn strictly at its natural size over its own white background with the
    ///     participant fill, and that the result names the format, the natural size, and the render operation.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_PlantUml_WritesTheDiagramAndDescribesIt()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "talk.puml", Sequence);
        var output = directory.Combine("talk.png");

        // Act
        using var document = RunJson(input, "-o", output, "--strict");
        using var scaled = RunJson(input, "-o", directory.Combine("talk2.png"), "--scale", "2", "--background", "transparent");

        // Assert: the size, the pixels, and the operation
        var root = document.RootElement;
        Assert.Equal("plantuml", root.GetProperty("input").GetProperty("format").GetString());
        var natural = root.GetProperty("input").GetProperty("natural");
        using var image = PngCodec.Load(output);
        Assert.Equal((natural.GetProperty("width").GetInt32(), natural.GetProperty("height").GetInt32()), (image.Width, image.Height));
        Assert.Equal(new Rgba32(255, 255, 255, 255), image[0, 0]);
        Assert.True(Count(image, ParticipantFill) > 200);
        var render = root.GetProperty("operations")[0];
        Assert.Equal("render", render.GetProperty("op").GetString());
        Assert.False(render.TryGetProperty("theme", out _));
        Assert.Empty(root.GetProperty("diagnostics").EnumerateArray());

        // Assert: twice the size over a transparent background
        using var large = PngCodec.Load(directory.Combine("talk2.png"));
        Assert.Equal((image.Width * 2, image.Height * 2), (large.Width, large.Height));
        Assert.Equal(0, large[0, 0].A);
        Assert.Equal("#00000000", scaled.RootElement.GetProperty("operations")[0].GetProperty("background").GetString());
    }

    /// <summary>
    ///     Test that <c>--theme</c> and <c>--positions</c>, which only a Mermaid diagram (or a chart) takes, are usage errors
    ///     on a PlantUML diagram for render and inspect, before anything is drawn.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_PlantUmlThemeAndPositions_AreUsageErrors()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "talk.puml", Sequence);

        // Act
        var theme = RunFailing(input, "--theme", "dark", "-o", directory.Combine("a.png"));
        var (positionsExit, _, positionsErr) = ToolHost.Invoke("render", input, "--positions", "all", "-o", directory.Combine("b.png"), "--json");
        var (inspectExit, _, inspectErr) = ToolHost.Invoke("inspect", "colors", input, "--theme", "dark");

        // Assert
        Assert.Equal(ErrorCodes.UsageError, theme.Code);
        Assert.Contains("--theme only applies to a Mermaid diagram", theme.Message, StringComparison.Ordinal);
        Assert.Contains("plantuml file", theme.Message, StringComparison.Ordinal);
        Assert.Equal(1, positionsExit);
        Assert.Contains("Error: usage-error: The option --positions only applies to a Mermaid diagram or a Vega-Lite chart", positionsErr, StringComparison.Ordinal);
        Assert.False(File.Exists(directory.Combine("a.png")));
        Assert.False(File.Exists(directory.Combine("b.png")));
        Assert.Equal(1, inspectExit);
        Assert.Contains("Error: usage-error: The option --theme only applies to a Mermaid diagram", inspectErr, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a diagram type this version does not draw renders an empty picture with the diagnostic
    ///     <c>plantuml-diagram-unsupported</c>, and fails with <c>--strict</c>.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_PlantUmlUnsupportedType_IsDiagnosticThenStrictError()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "cars.puml", "@startuml\nclass Car\n@enduml\n");

        // Act
        using var document = RunJson(input, "-o", directory.Combine("cars.png"));
        var strict = RunFailing(input, "--strict", "-o", directory.Combine("strict.png"));

        // Assert: one located diagnostic, an empty 20 by 20 picture, and a strict failure without output
        var diagnostic = Assert.Single(document.RootElement.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal(("plantuml", "unsupported", "plantuml-diagram-unsupported"), (diagnostic.GetProperty("source").GetString(), diagnostic.GetProperty("severity").GetString(), diagnostic.GetProperty("code").GetString()));
        Assert.True(diagnostic.GetProperty("location").GetProperty("line").GetInt32() >= 1);
        using var image = PngCodec.Load(directory.Combine("cars.png"));
        Assert.Equal((20, 20), (image.Width, image.Height));
        Assert.Equal(ErrorCodes.UnsupportedFeature, strict.Code);
        Assert.False(File.Exists(directory.Combine("strict.png")));
    }

    /// <summary>
    ///     Test that a warning of the library is the diagnostic <c>plantuml-warning</c> with the specific code in <c>detail</c>
    ///     and the position in <c>location</c>, and that invalid text is a <c>plantuml-error</c> with exit code 1, the error in
    ///     the JSON document, and no output.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_PlantUmlWarningAndSyntaxError_FollowTheContract()
    {
        // Arrange
        using var directory = new TempDirectory();
        var noisy = FixtureFactory.WriteText(directory.Path, "noisy.puml", "@startuml\nskinparam handwritten true\nAlice -> Bob : hi\n@enduml\n");
        var broken = FixtureFactory.WriteText(directory.Path, "broken.puml", "@startuml\nAlice -> Bob : hi\nthis is not plantuml at all\n@enduml\n");

        // Act
        using var document = RunJson(noisy, "-o", directory.Combine("noisy.png"));
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke("render", broken, "-o", directory.Combine("broken.png"), "--json");

        // Assert: the warning
        var warning = Assert.Single(document.RootElement.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal("plantuml-warning", warning.GetProperty("code").GetString());
        Assert.Equal("plantuml-skinparam-ignored", warning.GetProperty("detail").GetString());
        Assert.Equal(2, warning.GetProperty("location").GetProperty("line").GetInt32());

        // Assert: the error and no output
        Assert.Equal(1, exitCode);
        Assert.Contains("Error: plantuml-error: PlantUML syntax error:", stdErr, StringComparison.Ordinal);
        Assert.Contains("broken.puml:3:", stdErr, StringComparison.Ordinal);
        using var failure = JsonDocument.Parse(stdOut);
        Assert.Equal("plantuml-error", failure.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.False(File.Exists(directory.Combine("broken.png")));
    }
}
