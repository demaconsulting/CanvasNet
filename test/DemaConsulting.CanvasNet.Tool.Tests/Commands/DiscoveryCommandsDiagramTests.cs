using System.Text.Json;
using DemaConsulting.CanvasNet.Tool.Commands;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Unit tests for the DiscoveryCommands class with Mermaid diagrams: the listing of the format and the info description
///     of a diagram, of a diagram type this version does not draw, and of an input with a theme fragment.
/// </summary>
[Collection("Sequential")]
public class DiscoveryCommandsDiagramTests
{
    /// <summary>
    ///     A flowchart with frontmatter, an accessible title, three nodes, two edges and a subgraph.
    /// </summary>
    private const string Shipping =
        "---\ntitle: Shipping\n---\nflowchart TB\n    accTitle: Shipping steps\n    subgraph pack [Packing]\n        p1[Pick] --> p2[Box]\n    end\n    p2 --> s((Send))\n";

    /// <summary>
    ///     Runs the info command and captures what it writes.
    /// </summary>
    /// <param name="json"><see langword="true"/> to run with the global <c>--json</c> option.</param>
    /// <param name="args">The command arguments.</param>
    /// <returns>The captured standard output.</returns>
    private static string RunInfo(bool json, params string[] args)
    {
        using var context = Context.Create(json ? ["--json"] : []);
        var command = new DiscoveryCommands.Info();
        var (stdOut, _) = ToolHost.Capture(() => command.Run(context, args));
        return stdOut;
    }

    /// <summary>
    ///     Test that the formats listing names the diagram format as a vector kind that is not multi-page, with both of its
    ///     extensions and its help topic, in the text and in the JSON, and that the loader reads a file of that kind.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Formats_IncludesMermaidDiagram()
    {
        // Arrange
        using var directory = new TempDirectory();
        using var json = Context.Create(["--json"]);
        using var text = Context.Create([]);
        var command = new DiscoveryCommands.Formats("1.2.3", "4.5.6");

        // Act
        var (jsonOut, _) = ToolHost.Capture(() => command.Run(json, []));
        var (textOut, _) = ToolHost.Capture(() => command.Run(text, []));

        // Assert: the table row, the JSON row, and the text row
        var diagram = Assert.Single(DiscoveryCommands.InputFormats, r => r.Name == "mermaid");
        Assert.Equal(("diagram", true, false, "mermaid"), (diagram.Kind, diagram.Vector, diagram.MultiPage, diagram.HelpTopic));
        Assert.Equal([".mmd", ".mermaid"], diagram.Extensions);
        using var document = JsonDocument.Parse(jsonOut);
        var row = document.RootElement.GetProperty("inputs").EnumerateArray().Single(i => i.GetProperty("format").GetString() == "mermaid");
        Assert.Equal("diagram", row.GetProperty("kind").GetString());
        Assert.Equal([".mmd", ".mermaid"], row.GetProperty("extensions").EnumerateArray().Select(e => e.GetString()));
        Assert.Matches(@"mermaid\s+diagram\s+\.mmd \.mermaid\s+single\s+yes\s+help mermaid", textOut);
        Assert.Equal(SourceKind.Mermaid, SourceLoader.Probe(FixtureFactory.WriteSource(directory.Path, "mermaid")).Format);
    }

    /// <summary>
    ///     Test that the JSON description of a flowchart has an <c>image</c> object with its laid-out size and a <c>diagram</c>
    ///     object with its type, direction, titles and counts, and that the text description is one line with the same facts.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Info_Flowchart_ReportsTheDiagramObject()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "shipping.mmd", Shipping);
        var info = SourceLoader.Probe(path);

        // Act
        var json = RunInfo(true, path);
        var text = RunInfo(false, path).TrimEnd();

        // Assert: the JSON members
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal("mermaid", root.GetProperty("input").GetProperty("format").GetString());
        Assert.Equal(info.Width, root.GetProperty("image").GetProperty("width").GetInt32());
        Assert.Equal(info.Height, root.GetProperty("image").GetProperty("height").GetInt32());
        var diagram = root.GetProperty("diagram");
        Assert.Equal(
            ["type", "supported", "direction", "title", "accTitle", "nodes", "edges", "subgraphs"],
            diagram.EnumerateObject().Select(p => p.Name));
        Assert.Equal("flowchart", diagram.GetProperty("type").GetString());
        Assert.True(diagram.GetProperty("supported").GetBoolean());
        Assert.Equal("TB", diagram.GetProperty("direction").GetString());
        Assert.Equal("Shipping", diagram.GetProperty("title").GetString());
        Assert.Equal("Shipping steps", diagram.GetProperty("accTitle").GetString());
        Assert.Equal((3, 2, 1), (diagram.GetProperty("nodes").GetInt32(), diagram.GetProperty("edges").GetInt32(), diagram.GetProperty("subgraphs").GetInt32()));

        // Assert: one line of text with the same facts
        Assert.DoesNotContain('\n', text);
        Assert.Equal(
            $"{path}: mermaid, {info.Width}x{info.Height} px (laid out), flowchart TB, 3 nodes, 2 edges, 1 subgraph, title \"Shipping\", accTitle \"Shipping steps\", {info.Bytes} bytes",
            text);
    }

    /// <summary>
    ///     Test that a sequence diagram is described as supported, without a direction, with its title, and with its
    ///     participants, messages and boxes counted, which the text names as such.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Info_SequenceDiagram_ReportsParticipantsAndMessages()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(
            directory.Path,
            "talk.mmd",
            "sequenceDiagram\n    title Talk\n    box Office\n    participant Alice\n    end\n    Alice->>Bob: Hello\n    Bob-->>Alice: Hi\n");
        var info = SourceLoader.Probe(path);

        // Act
        var json = RunInfo(true, path);
        var text = RunInfo(false, path).TrimEnd();

        // Assert
        using var document = JsonDocument.Parse(json);
        var diagram = document.RootElement.GetProperty("diagram");
        Assert.Equal("sequence", diagram.GetProperty("type").GetString());
        Assert.True(diagram.GetProperty("supported").GetBoolean());
        Assert.False(diagram.TryGetProperty("direction", out _));
        Assert.Equal("Talk", diagram.GetProperty("title").GetString());
        Assert.Equal((2, 2, 1), (diagram.GetProperty("nodes").GetInt32(), diagram.GetProperty("edges").GetInt32(), diagram.GetProperty("subgraphs").GetInt32()));
        Assert.Equal(
            $"{path}: mermaid, {info.Width}x{info.Height} px (laid out), sequence, 2 participants, 2 messages, 1 box, title \"Talk\", {info.Bytes} bytes",
            text);
    }

    /// <summary>
    ///     Test that a class diagram is described as supported, with its direction and its classes, relations and
    ///     namespaces counted as nodes, edges and subgraphs (its notes and lollipop interfaces are not counted).
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Info_ClassDiagram_ReportsClassesAndRelations()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(
            directory.Path,
            "zoo.mmd",
            "classDiagram\n    direction LR\n    namespace Zoo {\n        class Duck\n    }\n    Animal <|-- Duck\n    Duck --() Swimmer\n    note for Duck \"quacks\"\n");
        var info = SourceLoader.Probe(path);

        // Act
        var json = RunInfo(true, path);
        var text = RunInfo(false, path).TrimEnd();

        // Assert
        using var document = JsonDocument.Parse(json);
        var diagram = document.RootElement.GetProperty("diagram");
        Assert.Equal("class", diagram.GetProperty("type").GetString());
        Assert.True(diagram.GetProperty("supported").GetBoolean());
        Assert.Equal("LR", diagram.GetProperty("direction").GetString());
        Assert.Equal((2, 2, 1), (diagram.GetProperty("nodes").GetInt32(), diagram.GetProperty("edges").GetInt32(), diagram.GetProperty("subgraphs").GetInt32()));
        Assert.Equal(
            $"{path}: mermaid, {info.Width}x{info.Height} px (laid out), class LR, 2 nodes, 2 edges, 1 subgraph, {info.Bytes} bytes",
            text);
    }

    /// <summary>
    ///     Test that a diagram type this version does not draw is described with <c>supported</c> false and no direction or
    ///     titles, so an agent learns before rendering that it would get an empty picture.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Info_PieChart_IsNotSupported()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "zoo.mmd", "pie\n    \"Ducks\" : 3\n");

        // Act
        var json = RunInfo(true, path);
        var text = RunInfo(false, path).TrimEnd();

        // Assert
        using var document = JsonDocument.Parse(json);
        var diagram = document.RootElement.GetProperty("diagram");
        Assert.Equal("pie", diagram.GetProperty("type").GetString());
        Assert.False(diagram.GetProperty("supported").GetBoolean());
        Assert.False(diagram.TryGetProperty("direction", out _));
        Assert.False(diagram.TryGetProperty("title", out _));
        Assert.Equal(0, diagram.GetProperty("nodes").GetInt32());
        Assert.Contains("pie (not drawn by this version), 0 nodes", text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that info accepts a theme fragment (it is checked, and does not change what is described) and refuses a theme
    ///     that does not exist with <c>image-fragment-invalid</c>.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Info_ThemeFragment_IsCheckedAndIgnored()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "shipping.mmd", Shipping);

        // Act
        var plain = RunInfo(true, path);
        var themed = RunInfo(true, path + "#theme=forest");
        var (exitCode, _, stdErr) = ToolHost.Invoke("info", path + "#theme=sunset");

        // Assert: the same diagram, with the fragment echoed; a theme that does not exist is an error
        using var plainDocument = JsonDocument.Parse(plain);
        using var themedDocument = JsonDocument.Parse(themed);
        Assert.Equal(plainDocument.RootElement.GetProperty("diagram").ToString(), themedDocument.RootElement.GetProperty("diagram").ToString());
        Assert.Equal("theme=forest", themedDocument.RootElement.GetProperty("input").GetProperty("fragment").GetString());
        Assert.Equal(1, exitCode);
        Assert.Contains("Error: image-fragment-invalid:", stdErr, StringComparison.Ordinal);
    }
}
