using DemaConsulting.CanvasNet.Mermaid.Er;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Er;

/// <summary>
///     Unit tests of <see cref="ErModel"/>: Mermaid's ER database (entities, aliases, attributes, relationships, style
///     classes and subgraphs), the mapping to the layout data of <c>getData</c>, and the limits.
/// </summary>
public class ErModelTests
{
    /// <summary>
    ///     Validates the entity ids (the number of entities before) and that repeated attribute blocks append.
    /// </summary>
    [Fact]
    public void ErModel_AddEntity_IdsCountEntitiesAndBlocksAppend()
    {
        // Act
        var model = ErFacts.Parse("erDiagram\nA {\n  int one\n}\nA {\n  int two\n}\nA ||--|| \"Entry Log\" : r\nA {\n  int three\n}\n");

        // Assert
        Assert.Equal(["entity-A-0", "entity-Entry Log-1"], model.Entities.Select(e => e.Id));
        Assert.Equal(["one", "two", "three"], model.Entities[0].Attributes.Select(a => a.Name));
        Assert.Equal(("entity-A-0", "entity-Entry Log-1"), (model.Relationships[0].EntityA, model.Relationships[0].EntityB));
    }

    /// <summary>
    ///     Validates that a relationship to a subgraph uses the subgraph's id and adds an entity named after it, which uses
    ///     up an entity number and a node but is not drawn.
    /// </summary>
    [Fact]
    public void ErModel_AddRelationship_ToASubgraph_UsesItsIdAndAddsAHiddenEntity()
    {
        // Arrange
        const string Text = "erDiagram\nsubgraph sub1\n  X\nend\nsub1 ||--o| Y : roleA\n";

        // Act
        var model = ErFacts.Parse(Text);
        var data = model.GetData(ErFacts.DefaultConfig(Text));

        // Assert
        Assert.Equal(["entity-X-0", "entity-sub1-1", "entity-Y-2"], model.Entities.Select(e => e.Id));
        Assert.Equal(("sub1", "entity-Y-2"), (model.Relationships[0].EntityA, model.Relationships[0].EntityB));
        Assert.Equal(["sub1", "entity-X-0", "entity-Y-2"], data.Nodes.Select(n => n.Id));
        Assert.Equal([0, null, 1], model.Entities.Select(e => e.ColorIndex));
        Assert.Equal("sub1", Assert.Single(data.Edges).Start);
    }

    /// <summary>
    ///     Validates the nodes of subgraphs: the trimmed values without duplicates, without the nodes of earlier subgraphs
    ///     (a nested subgraph ends first), with the trimmed id and the sanitized and trimmed title.
    /// </summary>
    [Fact]
    public void ErModel_AddSubgraph_NodesAreUniqueAndNotInEarlierSubgraphs()
    {
        // Act
        var model = ErFacts.Parse(
            "erDiagram\nsubgraph \" S \"[\" Sales area \"]\n  A\n  A ||--o{ B : r\n  subgraph T\n    B\n  end\nend\nsubgraph U\n  A\n  C\nend\n");

        // Assert
        Assert.Equal(["T", "S", "U"], model.Subgraphs.Select(s => s.Id));
        Assert.Equal(["B"], model.Subgraphs[0].Nodes);
        Assert.Equal(["A", "T"], model.Subgraphs[1].Nodes);
        Assert.Equal(["C"], model.Subgraphs[2].Nodes);
        Assert.Equal("Sales area", model.Subgraphs[1].Title);
    }

    /// <summary>
    ///     Validates that the clusters come first, the last subgraph first, and that each node's parent is the first
    ///     subgraph that holds it.
    /// </summary>
    [Fact]
    public void ErModel_GetData_SubgraphsComeFirstInReverseOrder()
    {
        // Arrange
        const string Text = "erDiagram\nsubgraph Outer[\"Outer area\"]\n  A\n  subgraph Inner\n    direction LR\n    B\n  end\nend\nsubgraph Other\n  C\nend\n";

        // Act
        var model = ErFacts.Parse(Text);
        var data = model.GetData(ErFacts.DefaultConfig(Text));

        // Assert
        Assert.Equal(["Other", "Outer", "Inner", "entity-A-0", "entity-B-1", "entity-C-2"], data.Nodes.Select(n => n.Id));
        Assert.Equal([null, null, "Outer", "Outer", "Inner", "Other"], data.Nodes.Select(n => n.ParentId));
        var outer = data.Nodes[1];
        Assert.Equal(("Outer area", "rect", true, 8.0), (outer.Label, outer.Shape, outer.IsGroup, outer.Padding));
        Assert.Equal("LR", data.Nodes[2].Dir);
    }

    /// <summary>
    ///     Validates the entity nodes and relationship edges of the layout data: the <c>erBox</c> with the alias and
    ///     attributes, and the edges with the ER markers of <c>cardB</c> at the start and <c>cardA</c> at the end.
    /// </summary>
    [Fact]
    public void ErModel_GetData_EntitiesAndRelationships_HaveMermaidsFields()
    {
        // Arrange
        const string Text = "erDiagram\np[\"Person\"] {\n  string name PK \"full\"\n}\np |o..|{ q : \"owns\"\nq u--o{ r : child\n";

        // Act
        var model = ErFacts.Parse(Text);
        var data = model.GetData(ErFacts.DefaultConfig(Text));

        // Assert
        var person = data.Nodes[0];
        Assert.Equal(("entity-p-0", "p", "erBox", "markdown", "default"), (person.Id, person.Label, person.Shape, person.LabelType, person.CssClasses));
        Assert.Equal("Person", person.ErBox!.Alias);
        var attribute = Assert.Single(person.ErBox.Attributes);
        Assert.Equal(("string", "name", "PK", "full"), (attribute.Type, attribute.Name, Assert.Single(attribute.Keys), attribute.Comment));
        var owns = data.Edges[0];
        Assert.Equal(("id_entity-p-0_entity-q-1_0", "owns", "zero_or_one", "one_or_more", "dashed"), (owns.Id, owns.Label, owns.ArrowTypeStart, owns.ArrowTypeEnd, owns.Pattern));
        Assert.Equal(("normal", "basis", "relationshipLine", "c", "normal", 1), (owns.Type, owns.Curve, owns.Classes, owns.LabelPos, owns.Thickness, owns.MinLen ?? 0));
        var child = data.Edges[1];
        Assert.Equal(("md_parent", "zero_or_more", "solid"), (child.ArrowTypeStart, child.ArrowTypeEnd, child.Pattern));
        Assert.Equal(("er", "TB", 140.0, 80.0), (data.DiagramType, data.Direction, data.NodeSpacing, data.RankSpacing));
        Assert.Equal(["only_one", "zero_or_one", "one_or_more", "zero_or_more"], data.Markers);
    }

    /// <summary>
    ///     Validates the styles: a class style with <c>color</c> gives a text style with <c>fill</c> as <c>bgFill</c>, the
    ///     compiled styles take the <c>default</c> class first, and inline styles apply to entities and subgraphs.
    /// </summary>
    [Fact]
    public void ErModel_GetData_StylesAndClasses_CompileLikeMermaid()
    {
        // Arrange
        const string Text = "erDiagram\nclassDef hot fill:#f96,color:#036, fill-color:red\nclassDef default stroke:#333\nA:::hot\nsubgraph S\n  B\nend\nclass S hot\nstyle S stroke:#f00\nstyle B stroke-dasharray: 5 5\n";

        // Act
        var model = ErFacts.Parse(Text);
        var data = model.GetData(ErFacts.DefaultConfig(Text));

        // Assert
        var hot = model.StyleClasses[0];
        Assert.Equal(["fill:#f96", "color:#036", "fill-color:red"], hot.Styles);
        Assert.Equal(["color:#036", "bgFill-color:red"], hot.TextStyles);
        var cluster = data.Nodes[0];
        Assert.Equal(("hot", "stroke:#f00"), (cluster.CssClasses, Assert.Single(cluster.CssStyles!)));
        Assert.Equal(["fill:#f96", "color:#036", "fill-color:red", "color:#036", "bgFill-color:red"], cluster.CssCompiledStyles);
        Assert.Equal(["stroke:#333", "fill:#f96", "color:#036", "fill-color:red", "color:#036", "bgFill-color:red"], data.Nodes[1].CssCompiledStyles);
        Assert.Equal(["stroke-dasharray:55"], data.Nodes[2].CssStyles);
    }

    /// <summary>
    ///     Validates that the spacing and the direction of the layout data follow the ER configuration and the direction
    ///     statement.
    /// </summary>
    [Fact]
    public void ErModel_GetData_SpacingAndDirection_FollowTheConfiguration()
    {
        // Arrange
        const string Text = "---\nconfig:\n  er:\n    nodeSpacing: 100\n    rankSpacing: 60\n---\nerDiagram\ndirection RL\nA ||--o{ B : r\n";

        // Act
        var model = ErFacts.Parse(Text);
        var data = model.GetData(ErFacts.DefaultConfig(Text));

        // Assert
        Assert.Equal(("RL", 100.0, 60.0), (data.Direction, data.NodeSpacing, data.RankSpacing));
    }

    /// <summary>
    ///     Validates that entities, the entities named after subgraphs and subgraphs count together against
    ///     <see cref="MermaidLimits.MaxNodes"/>, located at the statement that adds one too many.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="line">The line of the statement over the limit of 3.</param>
    [Theory]
    [InlineData("erDiagram\nA\nB\nC\nD\n", 5)]
    [InlineData("erDiagram\nsubgraph S\n  A\nend\nS ||--o{ B : r\n", 5)]
    [InlineData("erDiagram\nsubgraph S\n  A\n  subgraph T\n    B\n  end\nend\n", 2)]
    public void ErModel_Limits_EntitiesAndSubgraphs_CountAgainstMaxNodes(string text, int line)
    {
        // Arrange
        var limits = new MermaidLimits { MaxNodes = 3 };

        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => ErFacts.Parse(text, null, limits));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(line, ex.Location.Line);
        Assert.Contains("MermaidLimits.MaxNodes", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that relationships count against <see cref="MermaidLimits.MaxEdges"/>.
    /// </summary>
    [Fact]
    public void ErModel_Limits_TooManyRelationships_ThrowsLimitExceeded()
    {
        // Arrange
        var limits = new MermaidLimits { MaxEdges = 2 };

        // Act
        var accepted = ErFacts.Parse("erDiagram\nA ||--|| B : r\nB ||--|| A : s\n", null, limits);
        var ex = Assert.Throws<MermaidSyntaxException>(() =>
            ErFacts.Parse("erDiagram\nA ||--|| B : r\nB ||--|| A : s\nA ||--|| A : t\n", null, limits));

        // Assert
        Assert.Equal(2, accepted.Relationships.Count);
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(4, ex.Location.Line);
        Assert.Contains("MermaidLimits.MaxEdges", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the texts the model joins or derives are checked against
    ///     <see cref="MermaidLimits.MaxLabelLength"/>: a text style (<c>fill</c> as <c>bgFill</c>), the classes of an entity
    ///     or a subgraph joined with spaces, and compiled styles joined with semicolons.
    /// </summary>
    /// <param name="text">The text, with one joined text over the limit of 12.</param>
    /// <param name="line">The line of the statement located.</param>
    [Theory]
    [InlineData("erDiagram\nclassDef c fill:colors\n", 2)]
    [InlineData("erDiagram\nA\nclass A abc,def\n", 3)]
    [InlineData("erDiagram\nsubgraph S\nend\nclass S alpha,charlie\n", 4)]
    [InlineData("erDiagram\nclassDef a stroke:red\nclassDef b fill:red\nA\nclass A a,b\n", 5)]
    public void ErModel_Limits_JoinedTexts_ThrowLimitExceeded(string text, int line)
    {
        // Arrange
        var limits = new MermaidLimits { MaxLabelLength = 12 };

        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => ErFacts.Parse(text, null, limits));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(line, ex.Location.Line);
        Assert.Contains("MermaidLimits.MaxLabelLength", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that joined texts exactly at <see cref="MermaidLimits.MaxLabelLength"/> are accepted.
    /// </summary>
    [Fact]
    public void ErModel_Limits_JoinedTextsAtTheLimit_AreAccepted()
    {
        // Arrange
        var limits = new MermaidLimits { MaxLabelLength = 12 };

        // Act
        var model = ErFacts.Parse("erDiagram\nclassDef c fill:colo\nclassDef d s\nA\nclass A c,ab\nsubgraph S\nend\nclass S c,d\n", null, limits);

        // Assert
        Assert.Equal("default c ab", model.Entities[0].CssClasses);
        Assert.Equal(["fill:colo"], model.Entities[0].CssCompiledStyles);
        Assert.Equal(["fill:colo", "s"], model.Subgraphs[0].CssCompiledStyles);
    }

    /// <summary>
    ///     Validates the metadata the model reports: the drawn entities are counted as nodes (not the hidden entity that a
    ///     relationship to a subgraph adds), the relationships as edges and the subgraphs as subgraphs; the direction is
    ///     the last top-level statement's, the accessible title is kept, and the grammar declares no title.
    /// </summary>
    [Fact]
    public void ErModel_Metadata_CountsEntitiesRelationshipsAndSubgraphs()
    {
        // Arrange
        const string Text = "erDiagram\n    accTitle: Shop\n    direction LR\n    subgraph Sales\n        ORDER\n    end\n" +
            "    CUSTOMER ||--o{ ORDER : places\n    CUSTOMER ||--|| Sales : owns\n    direction RL\n";

        // Act
        var model = ErFacts.Parse(Text);

        // Assert
        Assert.Equal((2, 2, 1), (model.NodeCount, model.EdgeCount, model.SubgraphCount));
        Assert.Equal(3, model.Entities.Count);
        Assert.Equal(("RL", "Shop"), (model.Direction, model.AccTitle));
        Assert.Null(model.Title);
    }

    /// <summary>
    ///     Validates that a look other than classic and a layout other than dagre are reported in order, located at their
    ///     keys, with hints.
    /// </summary>
    [Fact]
    public void ErModel_ReportUnsupported_LookAndLayout_AreReportedInOrder()
    {
        // Arrange
        const string Text = "---\nconfig:\n  look: handDrawn\n  layout: elk\n---\nerDiagram\n    A ||--o{ B : has\n";
        var sink = new MermaidReportSink(100, throwOnUnsupported: false);
        var reports = new List<MermaidUnsupportedFeature>();

        // Act
        ErModel.ReportUnsupported(ErFacts.DefaultConfig(Text), sink);
        sink.Deliver(null, reports.Add);

        // Assert
        Assert.Equal(
            [(MermaidDiagnosticCodes.LookUnsupported, 3), (MermaidDiagnosticCodes.LayoutUnsupported, 4)],
            reports.Select(r => (r.Feature, r.Location.Line)));
        Assert.All(reports, r => Assert.NotNull(r.Hint));
    }

    /// <summary>
    ///     Validates the entity relationship renderer's frame of the drawing: the frontmatter title is drawn centered above
    ///     the drawing at the er section's title margin, and the view box is the drawn bounds grown by the padding of 8.
    /// </summary>
    [Fact]
    public void ErModel_Layout_Title_IsAboveTheDrawingInsideThePaddedViewBox()
    {
        // Arrange
        var document = MermaidDocument.Parse("---\ntitle: Shop\nconfig:\n  er:\n    titleTopMargin: 30\n---\nerDiagram\n    CUSTOMER\n");

        // Act
        var scene = document.LayoutScene(null, TestContext.Current.CancellationToken);

        // Assert
        var title = Assert.IsType<SceneText>(Assert.Single(SceneQuery.OfRole(scene, "title")));
        Assert.Equal((SceneTextAnchor.Middle, -30.0), (title.Anchor, title.Baseline));
        Assert.True(scene.ViewBox.Encloses(title.Box));
        var box = Assert.Single(SceneQuery.Nodes(scene)).Value.Box;
        Assert.Equal(box.X - 8, scene.ViewBox.X, 0.001);
        Assert.Equal(box.X + box.Width + 8, scene.ViewBox.X + scene.ViewBox.Width, 0.001);
        Assert.Equal(box.Y + box.Height + 8, scene.ViewBox.Y + scene.ViewBox.Height, 0.001);
    }
}
