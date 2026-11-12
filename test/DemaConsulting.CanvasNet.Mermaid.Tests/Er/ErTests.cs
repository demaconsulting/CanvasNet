namespace DemaConsulting.CanvasNet.Mermaid.Tests.Er;

/// <summary>
///     Subsystem tests of the Er subsystem: the parser and the model working together, from a whole text to the database,
///     the layout data and the located errors (the located limits of whole documents are in the hardening tests).
/// </summary>
public class ErTests
{
    /// <summary>
    ///     Gets the names of the original ER fixtures.
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
    ///     Gets the folder of the ER fixtures.
    /// </summary>
    private static string FixtureFolder => Path.Combine(AppContext.BaseDirectory, "Fixtures", "er");

    /// <summary>
    ///     Validates that every original ER fixture parses into layout data whose edges join its nodes, whose parents are
    ///     its clusters and whose entities are entity boxes.
    /// </summary>
    /// <param name="name">The fixture file name.</param>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Er_Parse_Fixture_GivesConsistentLayoutData(string name)
    {
        // Arrange
        var text = File.ReadAllText(Path.Combine(FixtureFolder, name));

        // Act
        var model = ErFacts.Parse(text, name);
        var data = model.GetData(ErFacts.DefaultConfig(text));

        // Assert
        var ids = data.Nodes.Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
        var clusters = data.Nodes.Where(n => n.IsGroup).Select(n => n.Id).ToHashSet(StringComparer.Ordinal);
        Assert.NotEmpty(model.Entities);
        Assert.All(data.Edges, edge => Assert.True(ids.Contains(edge.Start) && ids.Contains(edge.End), edge.Id));
        Assert.All(data.Nodes, node => Assert.True(node.ParentId is null || clusters.Contains(node.ParentId), node.Id));
        Assert.All(data.Nodes.Where(n => !n.IsGroup), node => Assert.Equal("erBox", node.Shape));
        Assert.All(data.Nodes.Where(n => !n.IsGroup), node => Assert.NotNull(node.ErBox));
    }

    /// <summary>
    ///     Validates a whole text with frontmatter and comments: entities with an alias and attributes, relationships in
    ///     symbols and words, a style class, a subgraph and the accessibility texts, and the layout data they map to.
    /// </summary>
    [Fact]
    public void Er_Parse_WholeText_GivesModelAndLayoutData()
    {
        // Arrange
        const string Text = "---\ntitle: Shop\n---\nerDiagram\n    %% a comment\n    accTitle: Shop model\n    accDescr: Customers and orders\n" +
            "    classDef warm fill:#f96\n    c[\"Customer\"]:::warm {\n        string name PK \"full name\"\n    }\n" +
            "    subgraph Sales[\"Sales area\"]\n        c ||--o{ ORDER : places\n    end\n    ORDER one or more optionally to zero or one INVOICE : \"billed by\"\n";

        // Act
        var model = ErFacts.Parse(Text);
        var data = model.GetData(ErFacts.DefaultConfig(Text));

        // Assert
        Assert.Equal(("Shop model", "Customers and orders"), (model.AccessibleTitle, model.AccessibleDescription));
        Assert.Equal(["c", "ORDER", "INVOICE"], model.Entities.Select(e => e.Name));
        Assert.Equal(["Sales", "entity-c-0", "entity-ORDER-1", "entity-INVOICE-2"], data.Nodes.Select(n => n.Id));
        Assert.Equal([null, "Sales", "Sales", null], data.Nodes.Select(n => n.ParentId));
        Assert.Equal(("Customer", "default warm"), (data.Nodes[1].ErBox!.Alias, data.Nodes[1].CssClasses));
        Assert.Equal(["fill:#f96"], data.Nodes[1].CssCompiledStyles);
        Assert.Equal(["only_one->zero_or_more:solid", "one_or_more->zero_or_one:dashed"], data.Edges.Select(e => $"{e.ArrowTypeStart}->{e.ArrowTypeEnd}:{e.Pattern}"));
        Assert.Equal(["places", "billed by"], data.Edges.Select(e => e.Label));
    }

    /// <summary>
    ///     Validates that a grammar error fails the whole parse, located at the line of the original text, frontmatter
    ///     included.
    /// </summary>
    [Fact]
    public void Er_Parse_GrammarError_FailsClosedWithLocation()
    {
        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() =>
            ErFacts.Parse("---\ntitle: T\n---\nerDiagram\nA ||--o{ B : r\nC ||--o{ D\n", "e.mmd"));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Equal("e.mmd", ex.Location.File);
        Assert.Equal(6, ex.Location.Line);
        Assert.Equal(["COLON", "STYLE_SEPARATOR"], ex.Expected);
    }

    /// <summary>
    ///     Validates that the document reports an entity relationship diagram as supported, with its metadata: the drawn
    ///     entities as nodes, relationships as edges, subgraphs as subgraphs and the direction that lays it out.
    /// </summary>
    [Fact]
    public void Er_Document_IsSupportedWithMetadata()
    {
        // Act
        var document = MermaidDocument.Parse("erDiagram\n    direction LR\n    subgraph Sales\n        ORDER\n    end\n" +
            "    CUSTOMER ||--o{ ORDER : places\n    ORDER ||--|{ LINE : holds\n");

        // Assert
        Assert.Equal(("er", true), (document.DiagramType, document.IsSupported));
        Assert.Equal((3, 2, 1), (document.NodeCount, document.EdgeCount, document.SubgraphCount));
        Assert.Equal("LR", document.Direction);
    }

    /// <summary>
    ///     Validates that every original entity relationship fixture lays out into a scene with a node for each drawn
    ///     entity, a cluster for each subgraph, the relationships as edges and a positive size.
    /// </summary>
    /// <param name="name">The fixture name.</param>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Er_Layout_Fixture_DrawsEntitiesAndRelationships(string name)
    {
        // Arrange
        var text = File.ReadAllText(Path.Combine(FixtureFolder, name));
        var model = ErFacts.Parse(text, name);

        // Act
        var scene = SceneQuery.Layout(text);

        // Assert
        Assert.True(scene.ViewBox.Width > 0 && scene.ViewBox.Height > 0);
        Assert.Equal(model.NodeCount, SceneQuery.Nodes(scene).Count);
        Assert.Equal(model.Subgraphs.Select(s => s.Id).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal), SceneQuery.Clusters(scene).Keys.Order(StringComparer.Ordinal));
        Assert.Equal(model.Relationships.Count, SceneQuery.Edges(scene).Count);
    }

    /// <summary>
    ///     Validates that what the subsystem does not draw as Mermaid does is reported in order at its location - the look
    ///     and the layout as unsupported features - and that the diagram is still drawn.
    /// </summary>
    [Fact]
    public void Er_Render_UnsupportedFeatures_AreReported()
    {
        // Arrange
        const string Text = "---\nconfig:\n  look: handDrawn\n  layout: elk\n---\nerDiagram\n    CUSTOMER ||--o{ ORDER : places\n";
        var document = MermaidDocument.Parse(Text, new MermaidOpenOptions { SourceName = "e.mmd" });
        var (options, warnings, unsupported) = MermaidTestHelpers.Collecting();

        // Act
        var size = document.Measure(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(size.Width > 16 && size.Height > 16);
        Assert.Equal(
            [(MermaidDiagnosticCodes.LookUnsupported, 3), (MermaidDiagnosticCodes.LayoutUnsupported, 4)],
            unsupported.Select(u => (u.Feature, u.Location.Line)));
        Assert.Empty(warnings);
    }
}
