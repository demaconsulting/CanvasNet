using System.Text.Json;
using DemaConsulting.CanvasNet.PlantUml;
using DemaConsulting.CanvasNet.Tool.Commands;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Unit tests for the DiscoveryCommands class with PlantUML diagrams: the listing of the format and the info
///     description of a sequence diagram, an activity diagram, and a diagram type this version does not draw.
/// </summary>
[Collection("Sequential")]
public class DiscoveryCommandsPlantUmlTests
{
    /// <summary>A small sequence diagram of two participants and two messages, one of them in a box.</summary>
    private const string Sequence = "@startuml\ntitle Talk\nbox Office\nparticipant Alice\nend box\nAlice -> Bob : Hello\nBob --> Alice : Hi\n@enduml\n";

    /// <summary>A small activity diagram with a decision.</summary>
    private const string Activity = "@startuml\nstart\n:Read;\nif (ok?) then (yes)\n  :Use;\nelse (no)\n  :Skip;\nendif\nstop\n@enduml\n";

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
    ///     Test that the formats listing names the PlantUML format as a vector diagram that is not multi-page, with its three
    ///     extensions and its help topic, in the text and in the JSON.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Formats_IncludesPlantUmlDiagram()
    {
        // Arrange
        using var json = Context.Create(["--json"]);
        using var text = Context.Create([]);
        var command = new DiscoveryCommands.Formats("1.2.3", "4.5.6");

        // Act
        var (jsonOut, _) = ToolHost.Capture(() => command.Run(json, []));
        var (textOut, _) = ToolHost.Capture(() => command.Run(text, []));

        // Assert: the table row, the JSON row, and the text row
        var diagram = Assert.Single(DiscoveryCommands.InputFormats, r => r.Name == "plantuml");
        Assert.Equal(("diagram", true, false, "plantuml"), (diagram.Kind, diagram.Vector, diagram.MultiPage, diagram.HelpTopic));
        Assert.Equal([".puml", ".plantuml", ".pu"], diagram.Extensions);
        using var document = JsonDocument.Parse(jsonOut);
        var row = document.RootElement.GetProperty("inputs").EnumerateArray().Single(i => i.GetProperty("format").GetString() == "plantuml");
        Assert.Equal("diagram", row.GetProperty("kind").GetString());
        Assert.Matches(@"plantuml\s+diagram\s+\.puml \.plantuml \.pu\s+single\s+yes\s+help plantuml", textOut);
    }

    /// <summary>
    ///     Test that info describes a sequence diagram with the <c>diagram</c> object (participants, messages, and boxes as
    ///     nodes, edges, and subgraphs; no direction and no accessible title) and one line of text that names them.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Info_PlantUmlSequence_ReportsParticipantsAndMessages()
    {
        // Arrange
        using var directory = new TempDirectory();
        var path = FixtureFactory.WriteText(directory.Path, "talk.puml", Sequence);
        var info = SourceLoader.Probe(path);

        // Act
        var json = RunInfo(true, path);
        var text = RunInfo(false, path).TrimEnd();

        // Assert: the JSON members
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal("plantuml", root.GetProperty("input").GetProperty("format").GetString());
        Assert.Equal(info.Width, root.GetProperty("image").GetProperty("width").GetInt32());
        var diagram = root.GetProperty("diagram");
        Assert.Equal(["type", "supported", "title", "nodes", "edges", "subgraphs"], diagram.EnumerateObject().Select(p => p.Name));
        Assert.Equal("sequence", diagram.GetProperty("type").GetString());
        Assert.True(diagram.GetProperty("supported").GetBoolean());
        Assert.Equal("Talk", diagram.GetProperty("title").GetString());
        Assert.Equal((2, 2, 1), (diagram.GetProperty("nodes").GetInt32(), diagram.GetProperty("edges").GetInt32(), diagram.GetProperty("subgraphs").GetInt32()));

        // Assert: one line of text with the same facts
        Assert.Equal(
            $"{path}: plantuml, {info.Width}x{info.Height} px (laid out), sequence, 2 participants, 2 messages, 1 box, title \"Talk\", {info.Bytes} bytes",
            text);
    }

    /// <summary>
    ///     Test that info describes an activity and a class diagram with the counts of the library, and a type this
    ///     version does not draw with <c>supported</c> false and zero counts.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Info_PlantUmlActivityClassAndLegacy_ReportTypeAndSupport()
    {
        // Arrange
        using var directory = new TempDirectory();
        var activity = FixtureFactory.WriteText(directory.Path, "flow.plantuml", Activity);
        var classes = FixtureFactory.WriteText(directory.Path, "cars.pu", "@startuml\npackage garage {\nclass Car\n}\nclass Wheel\nCar *-- Wheel\n@enduml\n");
        var legacy = FixtureFactory.WriteText(directory.Path, "legacy.puml", "@startuml\n(*) --> First\nFirst --> Second\n@enduml\n");
        var expected = PlantUmlDocument.Open(activity);

        // Act
        using var activityDocument = JsonDocument.Parse(RunInfo(true, activity));
        using var classDocument = JsonDocument.Parse(RunInfo(true, classes));
        using var legacyDocument = JsonDocument.Parse(RunInfo(true, legacy));
        var classText = RunInfo(false, classes);
        var legacyText = RunInfo(false, legacy);

        // Assert: the activity diagram and its counts
        var diagram = activityDocument.RootElement.GetProperty("diagram");
        Assert.Equal(("activity", true), (diagram.GetProperty("type").GetString(), diagram.GetProperty("supported").GetBoolean()));
        Assert.Equal(
            (expected.NodeCount, expected.EdgeCount, expected.SubgraphCount),
            (diagram.GetProperty("nodes").GetInt32(), diagram.GetProperty("edges").GetInt32(), diagram.GetProperty("subgraphs").GetInt32()));
        Assert.True(expected.NodeCount >= 5);

        // Assert: the class diagram is drawn: its classes, relationships, and packages
        var classDiagram = classDocument.RootElement.GetProperty("diagram");
        Assert.Equal(("class", true), (classDiagram.GetProperty("type").GetString(), classDiagram.GetProperty("supported").GetBoolean()));
        Assert.Equal((2, 1, 1), (classDiagram.GetProperty("nodes").GetInt32(), classDiagram.GetProperty("edges").GetInt32(), classDiagram.GetProperty("subgraphs").GetInt32()));
        Assert.Contains("class, 2 nodes, 1 edge", classText, StringComparison.Ordinal);

        // Assert: the legacy activity diagram is recognized and not drawn
        var unsupported = legacyDocument.RootElement.GetProperty("diagram");
        Assert.Equal(("activity-legacy", false, 0), (unsupported.GetProperty("type").GetString(), unsupported.GetProperty("supported").GetBoolean(), unsupported.GetProperty("nodes").GetInt32()));
        Assert.Contains("activity-legacy (not drawn by this version), 0 nodes", legacyText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that info describes a state diagram with the counts of the library: its states and pseudo-states,
    ///     transitions, and composite states.
    /// </summary>
    [Fact]
    public void DiscoveryCommands_Info_PlantUmlState_ReportsStatesTransitionsAndComposites()
    {
        // Arrange
        using var directory = new TempDirectory();
        var states = FixtureFactory.WriteText(directory.Path, "door.puml", "@startuml\n[*] --> Door\nstate Door {\n[*] --> Closed\nClosed --> Open : open\n}\nDoor --> [*]\n@enduml\n");

        // Act
        using var document = JsonDocument.Parse(RunInfo(true, states));
        var text = RunInfo(false, states);

        // Assert
        var diagram = document.RootElement.GetProperty("diagram");
        Assert.Equal(("state", true), (diagram.GetProperty("type").GetString(), diagram.GetProperty("supported").GetBoolean()));
        Assert.Equal((5, 4, 1), (diagram.GetProperty("nodes").GetInt32(), diagram.GetProperty("edges").GetInt32(), diagram.GetProperty("subgraphs").GetInt32()));
        Assert.Contains("state, 5 nodes, 4 edges", text, StringComparison.Ordinal);
    }
}
