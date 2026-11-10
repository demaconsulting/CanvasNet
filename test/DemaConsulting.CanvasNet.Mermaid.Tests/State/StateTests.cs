using System.Text;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.State;

/// <summary>
///     Subsystem tests of the State subsystem: the parser and the model working together, from a whole text to the
///     statement tree, the database, the layout data and the located errors and limits.
/// </summary>
public class StateTests
{
    /// <summary>
    ///     Gets the names of the original state fixtures.
    /// </summary>
    public static TheoryData<string> Fixtures
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var path in Directory.GetFiles(FixtureFolder, "*.mmd").Order(StringComparer.Ordinal))
            {
                data.Add(Path.GetFileName(path));
            }

            return data;
        }
    }

    /// <summary>
    ///     Gets the folder of the state fixtures.
    /// </summary>
    private static string FixtureFolder => Path.Combine(AppContext.BaseDirectory, "Fixtures", "state");

    /// <summary>
    ///     Validates that every original state fixture parses into layout data whose edges join its nodes and whose
    ///     parents are its clusters.
    /// </summary>
    /// <param name="name">The fixture file name.</param>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void State_Parse_Fixture_GivesConsistentLayoutData(string name)
    {
        // Arrange
        var text = File.ReadAllText(Path.Combine(FixtureFolder, name));

        // Act
        var model = StateFacts.Parse(text, name);
        var data = model.GetData(StateFacts.DefaultConfig(text));

        // Assert
        var ids = data.Nodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
        var clusters = data.Nodes.Where(n => n.IsGroup).Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
        Assert.NotEmpty(model.States);
        Assert.All(data.Edges, edge => Assert.True(ids.Contains(edge.Start) && ids.Contains(edge.End), edge.Id));
        Assert.All(data.Nodes, node => Assert.True(node.ParentId is null || clusters.Contains(node.ParentId), node.Id));
        Assert.All(data.Nodes, node => Assert.NotEmpty(node.Shape));
    }

    /// <summary>
    ///     Validates a whole text with frontmatter and comments: states, a composite state with regions, a note, a style
    ///     class, a link and the accessibility texts, and the layout data they map to.
    /// </summary>
    [Fact]
    public void State_Parse_WholeText_GivesModelAndLayoutData()
    {
        // Arrange
        const string Text = "---\ntitle: Keyboard\n---\nstateDiagram-v2\n    %% a comment\n    accTitle: Keys\n    accDescr: Lock states\n" +
            "    classDef warm fill:#f96\n    [*] --> Active:::warm\n    state Active {\n        [*] --> NumOff\n        --\n" +
            "        [*] --> CapsOff\n    }\n    note right of Active : two regions\n    click Active \"https://example.com\" \"Help\"\n";

        // Act
        var model = StateFacts.Parse(Text);
        var data = model.GetData(StateFacts.DefaultConfig(Text));

        // Assert
        Assert.Equal(("Keys", "Lock states"), (model.AccessibleTitle, model.AccessibleDescription));
        Assert.Equal(["root_start", "Active"], model.States.Select(s => s.Id));
        Assert.Equal(
            ["root_start", "Active", "divider-id-1", "divider-id-1_start", "NumOff", "id-random-1", "id-random-1_start", "CapsOff", "Active----parent", "Active----note-3"],
            data.Nodes.Select(n => n.Id));
        Assert.Equal(["fill:#f96"], data.Nodes[1].CssCompiledStyles);
        Assert.Equal(["edge0", "edge1", "edge2", "Active-Active----note-3"], data.Edges.Select(e => e.Id));
        Assert.Equal("\"https://example.com\"", Assert.Single(model.Links).Url);
    }

    /// <summary>
    ///     Validates that an error of Mermaid's database fails the whole parse, located at the line of the original text,
    ///     frontmatter included.
    /// </summary>
    [Fact]
    public void State_Parse_DatabaseError_FailsClosedWithLocation()
    {
        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() =>
            StateFacts.Parse("---\ntitle: T\n---\nstateDiagram-v2\nstate \"T\" as C {\n  A\n}\nC : more\n", "s.mmd"));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Equal(new MermaidLocation("s.mmd", 8, 1), ex.Location);
        Assert.Contains("[C]", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the legacy id (a non-default <c>state.defaultRenderer</c>) keeps Mermaid's id while its layout
    ///     data is that of the unified renderer.
    /// </summary>
    [Fact]
    public void State_Parse_LegacyStateId_KeepsItsIdAndTheUnifiedDiagramType()
    {
        // Arrange
        const string Text = "stateDiagram\n[*] --> A\nA --> B : go\n";

        // Act
        var model = StateFacts.Parse(Text, settings: new DetectorSettings("dagre-wrapper", "dagre-wrapper", "elk"));
        var data = model.GetData(StateFacts.DefaultConfig(Text));

        // Assert
        Assert.Equal(("state", true), (model.MermaidId, model.IsLegacy));
        Assert.Equal("stateDiagram", data.DiagramType);
        Assert.Equal(["root_start", "A", "B"], data.Nodes.Select(n => n.Id));
    }

    /// <summary>
    ///     Validates, with the default limits, that notes and their groups count against
    ///     <see cref="MermaidLimits.MaxNodes"/>: 700 states with a note each are 2,100 nodes, which fail at the note that
    ///     makes the 2,001st, located at its line.
    /// </summary>
    [Fact]
    public void State_Parse_ManyNotes_IsALocatedNodeLimit()
    {
        // Arrange
        var text = new StringBuilder("stateDiagram-v2\n");
        for (var i = 0; i < 700; i++)
        {
            text.Append(System.Globalization.CultureInfo.InvariantCulture, $"s{i}\nnote left of s{i} : n\n");
        }

        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => StateFacts.Parse(text.ToString(), "s.mmd"));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(1_335, ex.Location.Line);
    }

    /// <summary>
    ///     Validates, with the default limits, that concurrent regions count as nesting levels: 16 composite states that
    ///     each hold the next in a region are 32 levels and are accepted, and 17 fail at the composite state of the 33rd
    ///     level, though the parser allows 17 nested composite states.
    /// </summary>
    [Fact]
    public void State_Parse_NestedRegions_IsALocatedNestingLimit()
    {
        // Act
        var accepted = StateFacts.Parse(NestedRegions(16));
        var ex = Assert.Throws<MermaidSyntaxException>(() => StateFacts.Parse(NestedRegions(17), "s.mmd"));

        // Assert
        Assert.Equal(32, accepted.Nodes.Count(n => n.Shape == "divider"));
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(18, ex.Location.Line);
    }

    /// <summary>
    ///     Validates, with the default limits, that the descriptions drawn under a title are checked joined: 457 short
    ///     descriptions of one state make a block of 4,099 characters, which fails at the line that makes it too long.
    /// </summary>
    [Fact]
    public void State_Parse_ManyDescriptions_IsALocatedLabelLimit()
    {
        // Arrange
        var text = "stateDiagram-v2\n" + string.Concat(Enumerable.Repeat("S : abcd\n", 500));

        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => StateFacts.Parse(text, "s.mmd"));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(458, ex.Location.Line);
        Assert.Contains("4099 characters", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Builds composite states nested through their concurrent regions: each holds the next one, then a divider and a
    ///     state, so each adds two nesting levels.
    /// </summary>
    /// <param name="count">The number of composite states.</param>
    /// <returns>The text, with composite state <c>i</c> on line <c>i + 2</c>.</returns>
    private static string NestedRegions(int count)
    {
        var text = new StringBuilder("stateDiagram-v2\n");
        for (var i = 0; i < count; i++)
        {
            text.Append(System.Globalization.CultureInfo.InvariantCulture, $"state C{i} {{\n");
        }

        text.Append("x\n");
        for (var i = count - 1; i >= 0; i--)
        {
            text.Append(System.Globalization.CultureInfo.InvariantCulture, $"--\nz{i}\n}}\n");
        }

        return text.ToString();
    }
}
