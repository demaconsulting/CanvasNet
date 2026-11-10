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
        Assert.Equal("https://example.com/", Assert.Single(model.Links).Url);
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
    ///     Validates that the document reports a state diagram as supported, with its metadata: states as nodes,
    ///     transitions as edges, composite states as subgraphs (notes, note links and concurrent regions not counted) and
    ///     the direction that lays it out.
    /// </summary>
    [Fact]
    public void State_Document_IsSupportedWithMetadata()
    {
        // Act
        var document = MermaidDocument.Parse("stateDiagram-v2\n    direction LR\n    [*] --> Active\n    state Active {\n" +
            "        [*] --> NumOff\n        --\n        [*] --> CapsOff\n    }\n    Active --> [*]\n    note left of Active : keys\n");

        // Assert
        Assert.Equal(("state", true), (document.DiagramType, document.IsSupported));
        Assert.Equal((6, 4, 1), (document.NodeCount, document.EdgeCount, document.SubgraphCount));
        Assert.Equal("LR", document.Direction);
    }

    /// <summary>
    ///     Validates that every original state fixture lays out into a scene with a node for each state that is not a
    ///     composite state, a cluster for each composite state, transitions of the model as its edges (two self
    ///     transitions of one state are one edge, as in Mermaid's graph) and a positive size.
    /// </summary>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void State_Layout_Fixture_DrawsStatesAndTransitions(string name)
    {
        // Arrange
        var text = File.ReadAllText(Path.Combine(FixtureFolder, name));
        var model = StateFacts.Parse(text, name);

        // Act
        var scene = SceneQuery.Layout(text);

        // Assert
        Assert.True(scene.ViewBox.Width > 0 && scene.ViewBox.Height > 0);
        var nodes = SceneQuery.Nodes(scene);
        var clusters = SceneQuery.Clusters(scene);
        Assert.All(model.Nodes.Where(n => !n.IsGroup), node => Assert.Contains(node.DomId, nodes.Keys));
        Assert.All(model.Nodes.Where(n => n.IsGroup && n.Shape != "noteGroup"), node => Assert.Contains(node.DomId, clusters.Keys));
        var edges = SceneQuery.Edges(scene);
        Assert.NotEmpty(edges);
        Assert.All(edges.Keys, id => Assert.Contains(id, model.Edges.Select(edge => edge.Id)));
    }

    /// <summary>
    ///     Validates that what the subsystem does not draw as Mermaid does is reported in order at its location - the
    ///     legacy renderer, the look and the layout as unsupported features - and that the diagram is still drawn.
    /// </summary>
    [Fact]
    public void State_Render_UnsupportedFeatures_AreReported()
    {
        // Arrange
        const string Text = "---\nconfig:\n  look: handDrawn\n  layout: elk\n  state:\n    defaultRenderer: dagre-d3\n---\nstateDiagram\n    A --> B\n";
        var document = MermaidDocument.Parse(Text, new MermaidOpenOptions { SourceName = "s.mmd" });
        var (options, warnings, unsupported) = MermaidTestHelpers.Collecting();

        // Act
        var size = document.Measure(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(size.Width > 16 && size.Height > 16);
        Assert.Equal(
            [(MermaidDiagnosticCodes.LayoutUnsupported, 5), (MermaidDiagnosticCodes.LookUnsupported, 3), (MermaidDiagnosticCodes.LayoutUnsupported, 4)],
            unsupported.Select(u => (u.Feature, u.Location.Line)));
        Assert.Empty(warnings);
    }
}
