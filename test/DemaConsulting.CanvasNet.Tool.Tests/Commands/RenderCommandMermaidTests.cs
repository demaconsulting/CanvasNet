using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Mermaid;
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

    /// <summary>The white the tool draws a diagram over by default.</summary>
    private static readonly Rgba32 White = new(255, 255, 255, 255);

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
    ///     Counts the pixels of a picture that do not have a color.
    /// </summary>
    /// <param name="surface">The picture.</param>
    /// <param name="color">The color.</param>
    /// <returns>The number of pixels.</returns>
    private static int CountOther(Surface surface, Rgba32 color) => (surface.Width * surface.Height) - Count(surface, color);

    /// <summary>
    ///     Gets whether two pictures have the same size and the same pixels.
    /// </summary>
    /// <param name="expected">The first picture.</param>
    /// <param name="actual">The second picture.</param>
    /// <returns><see langword="true"/> when they are equal.</returns>
    private static bool SamePixels(Surface expected, Surface actual) =>
        (expected.Width, expected.Height) == (actual.Width, actual.Height)
        && Enumerable.Range(0, actual.Height).All(y => Enumerable.Range(0, actual.Width).All(x => expected[x, y] == actual[x, y]));

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
    ///     Test that a sequence diagram is drawn strictly at its natural size, with the participant boxes in the actor
    ///     fill of the default theme and no diagnostic.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_MermaidSequence_DrawsTheParticipants()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "talk.mmd", "sequenceDiagram\n    Alice->>Bob: Hello\n    Bob-->>Alice: Hi\n");
        var output = directory.Combine("talk.png");

        // Act
        using var document = RunJson(input, "-o", output, "--strict");

        // Assert: the natural size, a white corner, the actor fill, and no diagnostic
        var root = document.RootElement;
        var natural = root.GetProperty("input").GetProperty("natural");
        using var image = PngCodec.Load(output);
        Assert.Equal((natural.GetProperty("width").GetInt32(), natural.GetProperty("height").GetInt32()), (image.Width, image.Height));
        Assert.Equal(new Rgba32(255, 255, 255, 255), image[0, 0]);
        Assert.True(Count(image, DefaultNodeFill) > 2000);
        Assert.Empty(root.GetProperty("diagnostics").EnumerateArray());
    }

    /// <summary>
    ///     Test that a class diagram is drawn strictly at its natural size, with the class boxes in the node fill of the
    ///     default theme and no diagnostic.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_MermaidClass_DrawsTheClassBoxes()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "zoo.mmd", "classDiagram\n    class Animal {\n        +String name\n        +eat()\n    }\n    Animal <|-- Duck\n");
        var output = directory.Combine("zoo.png");

        // Act
        using var document = RunJson(input, "-o", output, "--strict");

        // Assert: the natural size, a white corner, the box fill, and no diagnostic
        var root = document.RootElement;
        var natural = root.GetProperty("input").GetProperty("natural");
        using var image = PngCodec.Load(output);
        Assert.Equal((natural.GetProperty("width").GetInt32(), natural.GetProperty("height").GetInt32()), (image.Width, image.Height));
        Assert.Equal(new Rgba32(255, 255, 255, 255), image[0, 0]);
        Assert.True(Count(image, DefaultNodeFill) > 2000);
        Assert.Empty(root.GetProperty("diagnostics").EnumerateArray());
    }

    /// <summary>
    ///     Test that a state diagram is drawn strictly at its natural size, with the states in the state fill of the
    ///     default theme and no diagnostic.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_MermaidState_DrawsTheStates()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "lamp.mmd", "stateDiagram-v2\n    [*] --> Off\n    Off --> On : switch\n    On --> Off : switch\n    On --> [*]\n");
        var output = directory.Combine("lamp.png");

        // Act
        using var document = RunJson(input, "-o", output, "--strict");

        // Assert: the natural size, a white corner, the state fill, and no diagnostic
        var root = document.RootElement;
        var natural = root.GetProperty("input").GetProperty("natural");
        using var image = PngCodec.Load(output);
        Assert.Equal((natural.GetProperty("width").GetInt32(), natural.GetProperty("height").GetInt32()), (image.Width, image.Height));
        Assert.Equal(new Rgba32(255, 255, 255, 255), image[0, 0]);
        Assert.True(Count(image, DefaultNodeFill) > 1000);
        Assert.Empty(root.GetProperty("diagnostics").EnumerateArray());
    }

    /// <summary>
    ///     Test that an entity relationship diagram is drawn strictly at its natural size, with the entities in the node fill
    ///     of the default theme and no diagnostic.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_MermaidEr_DrawsTheEntities()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "shop.mmd", "erDiagram\n    CUSTOMER ||--o{ ORDER : places\n    ORDER {\n        int id PK\n        string note\n    }\n");
        var output = directory.Combine("shop.png");

        // Act
        using var document = RunJson(input, "-o", output, "--strict");

        // Assert: the natural size, a white corner, the entity fill, and no diagnostic
        var root = document.RootElement;
        var natural = root.GetProperty("input").GetProperty("natural");
        using var image = PngCodec.Load(output);
        Assert.Equal((natural.GetProperty("width").GetInt32(), natural.GetProperty("height").GetInt32()), (image.Width, image.Height));
        Assert.Equal(new Rgba32(255, 255, 255, 255), image[0, 0]);
        Assert.True(Count(image, DefaultNodeFill) > 1000);
        Assert.Empty(root.GetProperty("diagnostics").EnumerateArray());
    }

    /// <summary>
    ///     Test that a pie chart is drawn strictly at its natural size, pixel for pixel as the library draws it over white,
    ///     with no diagnostic.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_MermaidPie_DrawsTheSlices()
    {
        // Arrange
        const string Text = "pie title Pets\n    \"Dogs\" : 386\n    \"Cats\" : 85\n    \"Rats\" : 15\n";
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "pets.mmd", Text);
        var output = directory.Combine("pets.png");

        // Act
        using var document = RunJson(input, "-o", output, "--strict");
        using var expected = MermaidDocument.Parse(Text).Render(new MermaidRenderOptions { Background = White }, TestContext.Current.CancellationToken);

        // Assert: the natural size, a white corner, a picture that is not blank and is the library's, and no diagnostic
        var root = document.RootElement;
        var natural = root.GetProperty("input").GetProperty("natural");
        using var image = PngCodec.Load(output);
        Assert.Equal((natural.GetProperty("width").GetInt32(), natural.GetProperty("height").GetInt32()), (image.Width, image.Height));
        Assert.Equal(White, image[0, 0]);
        Assert.True(CountOther(image, White) > image.Width * image.Height / 10);
        Assert.True(SamePixels(expected, image), "The tool's pie chart differs from the library's.");
        Assert.Empty(root.GetProperty("diagnostics").EnumerateArray());
    }

    /// <summary>
    ///     Test that a gantt chart is drawn strictly at its natural size, pixel for pixel as the library draws it without a
    ///     clock (no today line, though a clock inside the chart would draw one), with no diagnostic.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_MermaidGantt_DrawsTheTasksWithoutATodayLine()
    {
        // Arrange
        const string Text = "gantt\n    dateFormat YYYY-MM-DD\n    section Build\n    Design    :des, 2024-06-03, 5d\n    Implement :crit, after des, 8d\n";
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "plan.mmd", Text);
        var output = directory.Combine("plan.png");
        var diagram = MermaidDocument.Parse(Text);

        // Act
        using var document = RunJson(input, "-o", output, "--strict");
        using var expected = diagram.Render(new MermaidRenderOptions { Background = White }, TestContext.Current.CancellationToken);
        using var withToday = diagram.Render(
            new MermaidRenderOptions { Background = White, Now = new DateTimeOffset(2024, 6, 6, 12, 0, 0, TimeSpan.Zero) },
            TestContext.Current.CancellationToken);

        // Assert: the natural size, a picture that is not blank and is the library's without a clock, and no diagnostic
        var root = document.RootElement;
        var natural = root.GetProperty("input").GetProperty("natural");
        using var image = PngCodec.Load(output);
        Assert.Equal((natural.GetProperty("width").GetInt32(), natural.GetProperty("height").GetInt32()), (image.Width, image.Height));
        Assert.True(CountOther(image, White) > image.Width * image.Height / 20);
        Assert.True(SamePixels(expected, image), "The tool's gantt chart differs from the library's.");
        Assert.False(SamePixels(withToday, image), "The tool's gantt chart has a today line.");
        Assert.Empty(root.GetProperty("diagnostics").EnumerateArray());
    }

    /// <summary>
    ///     Test that a gantt chart that needs today (a <c>todayMarker</c> style) is drawn with the warning
    ///     <c>mermaid-gantt-today-unavailable</c> as the detail of <c>mermaid-warning</c>, also with <c>--strict</c>, and
    ///     that a date Mermaid cannot read is a located <c>mermaid-error</c> without a picture.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_MermaidGanttTodayAndInvalidDate_AreWarningThenError()
    {
        // Arrange
        using var directory = new TempDirectory();
        var marked = FixtureFactory.WriteText(directory.Path, "marked.mmd", "gantt\n    todayMarker stroke:#f00\n    Design :des, 2024-06-03, 5d\n");
        var invalid = FixtureFactory.WriteText(directory.Path, "invalid.mmd", "gantt\n    Design :des, Jan 5 2024, 5d\n");

        // Act
        using var document = RunJson(marked, "-o", directory.Combine("marked.png"), "--strict");
        var (exitCode, stdOut, stdErr) = ToolHost.Invoke("render", invalid, "-o", directory.Combine("invalid.png"), "--json");

        // Assert: the warning at the todayMarker statement, and the picture
        var warning = Assert.Single(document.RootElement.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal("mermaid-warning", warning.GetProperty("code").GetString());
        Assert.Equal("mermaid-gantt-today-unavailable", warning.GetProperty("detail").GetString());
        Assert.Equal(2, warning.GetProperty("location").GetProperty("line").GetInt32());
        Assert.True(File.Exists(directory.Combine("marked.png")));

        // Assert: the error names the date and its line, and nothing is written
        Assert.Equal(1, exitCode);
        Assert.Contains("Error: mermaid-error:", stdErr, StringComparison.Ordinal);
        Assert.Contains("Invalid date", stdErr, StringComparison.Ordinal);
        Assert.Contains("invalid.mmd:2:", stdErr, StringComparison.Ordinal);
        using var failure = JsonDocument.Parse(stdOut);
        Assert.Equal("mermaid-error", failure.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.False(File.Exists(directory.Combine("invalid.png")));
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
    ///     <c>mermaid-diagram-unsupported</c>, which says that the type is not supported, and fails with <c>--strict</c>.
    /// </summary>
    [Fact]
    public void RenderCommand_Run_MermaidJourney_IsUnsupportedDiagnosticThenStrictError()
    {
        // Arrange
        using var directory = new TempDirectory();
        var input = FixtureFactory.WriteText(directory.Path, "zoo.mmd", "journey\n    title Ducks\n");

        // Act
        using var document = RunJson(input, "-o", directory.Combine("zoo.png"));
        var strict = RunFailing(input, "--strict", "-o", directory.Combine("strict.png"));

        // Assert: one diagnostic with its source, severity, code, message and location
        var diagnostic = Assert.Single(document.RootElement.GetProperty("diagnostics").EnumerateArray());
        Assert.Equal("mermaid", diagnostic.GetProperty("source").GetString());
        Assert.Equal("unsupported", diagnostic.GetProperty("severity").GetString());
        Assert.Equal("mermaid-diagram-unsupported", diagnostic.GetProperty("code").GetString());
        Assert.Contains("journey diagrams are not supported", diagnostic.GetProperty("message").GetString(), StringComparison.Ordinal);
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
