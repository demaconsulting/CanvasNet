using DemaConsulting.CanvasNet.Mermaid.Mindmap;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Mindmap;

/// <summary>
///     Subsystem tests of the Mindmap subsystem (<c>MindmapParser</c> and <c>MindmapRenderer</c> together): a whole text
///     gives the diagram and the scene, with one drawn node per parsed node and one edge per parent link, laid out the
///     same way every time, what is not drawn is reported, and a text that does not follow the grammar fails closed,
///     located.
/// </summary>
public sealed class MindmapTests
{
    /// <summary>
    ///     Gets mindmaps of several shapes and sizes.
    /// </summary>
    public static TheoryData<string> Texts =>
    [
        "mindmap\n  root((mindmap))\n    Origins\n      Long history\n      Popularization\n    Research\n      On effectiveness<br/>and features\n      On Automatic creation\n    Tools\n      Pen and paper\n      Mermaid\n",
        "mindmap\nRoot\n  A[Square]\n    A1\n  B(Rounded)\n  C{{Hexagon}}\n    C1\n    C2\n  D((Circle))\n  E))Bang((\n  F)Cloud(\n",
        "---\nconfig:\n  htmlLabels: false\n  mindmap:\n    padding: 7\n    maxNodeWidth: 90\n---\nmindmap\n  root\n    a long label that wraps at ninety\n    b(rounded)\n    c{{hex}}\n      c1\n",
        "mindmap\nsingle\n",
    ];

    /// <summary>
    ///     Validates that a text parses to a mindmap and lays out with one node group per node and one edge per parent
    ///     link, every node inside the view box, the same way twice.
    /// </summary>
    /// <param name="text">The text.</param>
    [Theory]
    [MemberData(nameof(Texts))]
    public void CanvasNetMermaid_Mindmap_Text_ParsesAndLaysOutConsistently(string text)
    {
        // Arrange
        var document = MermaidDocument.Parse(text);

        // Act
        var scene = SceneQuery.Layout(text);
        var again = SceneQuery.Layout(text);

        // Assert
        var diagram = Assert.IsType<MindmapDiagram>(document.Diagram);
        var nodes = SceneQuery.Nodes(scene);
        Assert.Equal(diagram.Nodes.Select(node => "node_" + node.Id).Order(StringComparer.Ordinal), nodes.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(diagram.Edges.Select(edge => edge.Id).Order(StringComparer.Ordinal), SceneQuery.Edges(scene).Keys.Order(StringComparer.Ordinal));
        Assert.All(nodes.Values, node => Assert.True(scene.ViewBox.Encloses(node.Box), $"{node.Box} outside {scene.ViewBox}"));
        Assert.Equal(scene.ViewBox, again.ViewBox);
        Assert.Equal(nodes, SceneQuery.Nodes(again));
    }

    /// <summary>
    ///     Validates the whole pipeline on a text: the metadata, each node's label as drawn (markdown read), and each
    ///     edge from its parent to its child.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Mindmap_Text_GivesTheDiagramAndTheScene()
    {
        // Arrange
        const string Text = "mindmap\n  Root\n    **Bold** child\n    other[Square]\n      leaf\n";

        // Act
        var document = MermaidDocument.Parse(Text);
        var scene = SceneQuery.Layout(Text);

        // Assert
        Assert.Equal(("mindmap", true, 4, 3, 0), (document.DiagramType, document.IsSupported, document.NodeCount, document.EdgeCount, document.SubgraphCount));
        var texts = scene.Walk().Select(entry => entry.Item).OfType<SceneText>().Select(text => string.Concat(text.Runs.Select(run => run.Text))).ToList();
        Assert.Equal(["Root", "Bold child", "Square", "leaf"], texts);
        var bold = scene.Walk().Select(entry => entry.Item).OfType<SceneText>().First(text => text.Runs.Any(run => run.Text.Contains("Bold", StringComparison.Ordinal)));
        Assert.True(bold.Runs[0].Font.Bold);
        Assert.Equal(["edge_0_1", "edge_0_2", "edge_2_3"], SceneQuery.Edges(scene).Keys);
    }

    /// <summary>
    ///     Validates that what a mindmap asks for but is not drawn (a layout other than cose-bilkent and a node's icon) is
    ///     reported in order, located, and that the whole mindmap is still drawn.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Mindmap_UnsupportedFeatures_AreReportedAndDrawn()
    {
        // Arrange
        const string Text = "---\nconfig:\n  layout: elk\n---\nmindmap\n  Root\n    Books\n    ::icon(fa fa-book)\n";
        var (options, warnings, unsupported) = MermaidTestHelpers.Collecting();

        // Act
        var scene = SceneQuery.Layout(Text, options);

        // Assert
        Assert.Equal(2, SceneQuery.Nodes(scene).Count);
        Assert.Single(SceneQuery.Edges(scene));
        Assert.Empty(warnings);
        Assert.Equal(
            [(MermaidDiagnosticCodes.LayoutUnsupported, 3), (MermaidDiagnosticCodes.IconUnsupported, 8)],
            unsupported.Select(report => (report.Feature, report.Location.Line)));
    }

    /// <summary>
    ///     Validates that a second root, and text the grammar does not accept, are refused when the document is opened,
    ///     located in the original text with a hint.
    /// </summary>
    [Fact]
    public void CanvasNetMermaid_Mindmap_InvalidText_FailsClosedWithLocation()
    {
        // Arrange
        var options = new MermaidOpenOptions { SourceName = "m.mmd" };

        // Act
        var root = Assert.Throws<MermaidSyntaxException>(() => MermaidDocument.Parse("---\ntitle: T\n---\nmindmap\n  a\n  b\n", options));
        var grammar = Assert.Throws<MermaidSyntaxException>(() => MermaidDocument.Parse("mindmap\n  a\n    b[unclosed\n", options));

        // Assert
        Assert.Equal((MermaidDiagnosticCodes.SyntaxError, "m.mmd", 6), (root.Code, root.Location.File, root.Location.Line));
        Assert.Contains("There can be only one root", root.Message, StringComparison.Ordinal);
        Assert.NotNull(root.Hint);
        Assert.Equal(MermaidDiagnosticCodes.SyntaxError, grammar.Code);
        Assert.NotEmpty(grammar.Expected);
        Assert.NotNull(grammar.Hint);
    }
}
