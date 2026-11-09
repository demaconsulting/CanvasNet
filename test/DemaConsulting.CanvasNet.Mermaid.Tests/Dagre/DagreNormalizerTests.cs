using DemaConsulting.CanvasNet.Mermaid.Dagre;
using DemaConsulting.CanvasNet.Mermaid.Graphlib;
using static DemaConsulting.CanvasNet.Mermaid.Tests.Dagre.DagreTestGraphs;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Dagre;

/// <summary>
///     Unit tests of <see cref="DagreNormalizer"/>, with the cases of dagre's <c>normalize.test.js</c>.
/// </summary>
public class DagreNormalizerTests
{
    /// <summary>
    ///     Validates that a short edge is not changed.
    /// </summary>
    [Fact]
    public void DagreNormalizer_Run_ShortEdge_Unchanged()
    {
        // Arrange
        var g = Layout(compound: true);
        g.SetNode("a", Ranked(0)).SetNode("b", Ranked(1)).SetEdge("a", "b", new LayoutEdge());

        // Act
        DagreNormalizer.Run(g, Meter());

        // Assert
        Assert.Equal([new GraphEdge("a", "b")], g.Edges());
        Assert.Empty(g.GraphLabel!.DummyChains);
    }

    /// <summary>
    ///     Validates that a long edge is split through zero-size dummies, the weight is kept, and the label rank gets the
    ///     label size.
    /// </summary>
    [Fact]
    public void DagreNormalizer_Run_LongEdge_SplitThroughDummies()
    {
        // Arrange
        var g = Layout(compound: true);
        g.SetNode("a", Ranked(0)).SetNode("b", Ranked(4));
        g.SetEdge("a", "b", new LayoutEdge { Width = 20, Height = 10, LabelRank = 2, Weight = 2, LabelPos = "c" });

        // Act
        DagreNormalizer.Run(g, Meter());

        // Assert
        var d1 = g.Successors("a")!.Single();
        var d2 = g.Successors(d1)!.Single();
        var d3 = g.Successors(d2)!.Single();
        Assert.Equal(["b"], g.Successors(d3)!);
        Assert.Equal(("edge", 1.0, 0.0, 0.0), (g.Node(d1)!.Dummy, g.Node(d1)!.Rank, g.Node(d1)!.Width, g.Node(d1)!.Height));
        Assert.Equal(("edge-label", 2.0, 20.0, 10.0, "c"), (g.Node(d2)!.Dummy, g.Node(d2)!.Rank, g.Node(d2)!.Width, g.Node(d2)!.Height, g.Node(d2)!.LabelPos));
        Assert.Equal(2.0, g.Edge("a", d1)!.Weight);
        Assert.Equal([d1], g.GraphLabel!.DummyChains);
    }

    /// <summary>
    ///     Validates that undo restores the edge with its label, the dummy positions as its route and the label position.
    /// </summary>
    [Fact]
    public void DagreNormalizer_Undo_Chain_RestoresEdgeWithRoute()
    {
        // Arrange
        var g = Layout(compound: true);
        var label = new LayoutEdge { Width = 20, Height = 10, LabelRank = 1 };
        g.SetNode("a", Ranked(0)).SetNode("b", Ranked(2)).SetEdge("a", "b", label);
        DagreNormalizer.Run(g, Meter());
        var dummy = g.Successors("a")![0];
        g.Node(dummy)!.X = 5;
        g.Node(dummy)!.Y = 10;

        // Act
        DagreNormalizer.Undo(g, Meter());

        // Assert
        Assert.Equal([new GraphEdge("a", "b")], g.Edges());
        Assert.Same(label, g.Edge("a", "b"));
        var point = Assert.Single(label.Points!);
        Assert.Equal((5.0, 10.0), (point.X, point.Y));
        Assert.Equal((5.0, 10.0, 20.0, 10.0), (label.X, label.Y, label.Width, label.Height));
        Assert.Equal(2, g.NodeCount);
    }
}
