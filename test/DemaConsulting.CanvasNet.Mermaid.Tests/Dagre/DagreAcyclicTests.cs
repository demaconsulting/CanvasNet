using DemaConsulting.CanvasNet.Mermaid.Dagre;
using DemaConsulting.CanvasNet.Mermaid.Graphlib;
using static DemaConsulting.CanvasNet.Mermaid.Tests.Dagre.DagreTestGraphs;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Dagre;

/// <summary>
///     Unit tests of <see cref="DagreAcyclic"/>, with the cases of dagre's <c>acyclic.test.js</c> and
///     <c>greedy-fas.test.js</c>.
/// </summary>
public class DagreAcyclicTests
{
    /// <summary>
    ///     Validates that an acyclic graph is not changed, with every breaker.
    /// </summary>
    /// <param name="acyclicer">The breaker.</param>
    [Theory]
    [InlineData("greedy")]
    [InlineData("dfs")]
    [InlineData("unknown-should-still-work")]
    public void DagreAcyclic_Run_AcyclicGraph_Unchanged(string acyclicer)
    {
        // Arrange
        var g = Layout();
        g.GraphLabel!.Acyclicer = acyclicer;
        g.SetPath(["a", "b", "d"]).SetPath(["a", "c", "d"]);

        // Act
        DagreAcyclic.Run(g, Meter());

        // Assert
        Assert.Equal(
            [new GraphEdge("a", "b"), new GraphEdge("a", "c"), new GraphEdge("b", "d"), new GraphEdge("c", "d")],
            g.Edges().OrderBy(e => e.V, StringComparer.Ordinal).ThenBy(e => e.W, StringComparer.Ordinal));
    }

    /// <summary>
    ///     Validates that cycles are broken, a two-cycle into two parallel edges, with every breaker.
    /// </summary>
    /// <param name="acyclicer">The breaker.</param>
    [Theory]
    [InlineData("greedy")]
    [InlineData("dfs")]
    public void DagreAcyclic_Run_Cycles_Broken(string acyclicer)
    {
        // Arrange
        var square = Layout();
        square.GraphLabel!.Acyclicer = acyclicer;
        square.SetPath(["a", "b", "c", "d", "a"]);
        var pair = Layout();
        pair.GraphLabel!.Acyclicer = acyclicer;
        pair.SetPath(["a", "b", "a"]);

        // Act
        DagreAcyclic.Run(square, Meter());
        DagreAcyclic.Run(pair, Meter());

        // Assert
        Assert.Empty(GraphAlgorithms.FindCycles(square));
        Assert.Empty(GraphAlgorithms.FindCycles(pair));
        Assert.Equal(2, pair.EdgeCount);
        Assert.Equal(2, pair.HasEdge("a", "b") ? pair.OutEdges("a", "b")!.Length : pair.OutEdges("b", "a")!.Length);
    }

    /// <summary>
    ///     Validates that undo restores the reversed edges with their labels and names.
    /// </summary>
    /// <param name="acyclicer">The breaker.</param>
    [Theory]
    [InlineData("greedy")]
    [InlineData("dfs")]
    public void DagreAcyclic_Undo_ReversedEdges_Restored(string acyclicer)
    {
        // Arrange
        var g = Layout();
        g.GraphLabel!.Acyclicer = acyclicer;
        var ab = new LayoutEdge { MinLen = 2, Weight = 3 };
        var ba = new LayoutEdge { MinLen = 3, Weight = 4 };
        g.SetEdge("a", "b", ab).SetEdge("b", "a", ba, "named");

        // Act
        DagreAcyclic.Run(g, Meter());
        DagreAcyclic.Undo(g);

        // Assert
        Assert.Same(ab, g.Edge("a", "b"));
        Assert.Same(ba, g.Edge("b", "a", "named"));
        Assert.Equal(2, g.EdgeCount);
        Assert.False(ab.Reversed || ba.Reversed);
    }

    /// <summary>
    ///     Validates that the greedy breaker prefers to reverse a light edge.
    /// </summary>
    [Fact]
    public void DagreAcyclic_Run_Greedy_BreaksAtLowWeightEdge()
    {
        // Arrange
        var g = Layout().SetDefaultEdgeLabel((_, _, _) => new LayoutEdge { MinLen = 1, Weight = 2 });
        g.GraphLabel!.Acyclicer = "greedy";
        g.SetPath(["a", "b", "c", "d", "a"]);
        g.SetEdge("c", "d", new LayoutEdge { MinLen = 1, Weight = 1 });

        // Act
        DagreAcyclic.Run(g, Meter());

        // Assert
        Assert.Empty(GraphAlgorithms.FindCycles(g));
        Assert.False(g.HasEdge("c", "d"));
    }

    /// <summary>
    ///     Validates the greedy feedback arc set on empty, single-node and acyclic graphs.
    /// </summary>
    [Fact]
    public void DagreAcyclic_GreedyFas_NoCycles_Empty()
    {
        // Arrange
        var empty = Layout();
        var single = Layout().SetNode("a");
        var acyclic = Layout().SetEdge("a", "b").SetEdge("b", "c").SetEdge("b", "d").SetEdge("a", "e");

        // Act / Assert
        Assert.Empty(DagreAcyclic.GreedyFas(empty, _ => 1, Meter()));
        Assert.Empty(DagreAcyclic.GreedyFas(single, _ => 1, Meter()));
        Assert.Empty(DagreAcyclic.GreedyFas(acyclic, _ => 1, Meter()));
    }

    /// <summary>
    ///     Validates that the greedy set breaks every cycle within the heuristic's bound.
    /// </summary>
    [Fact]
    public void DagreAcyclic_GreedyFas_TwoFourNodeCycles_WithinBound()
    {
        // Arrange
        var g = Layout(multigraph: false);
        g.SetEdge("n1", "n2").SetPath(["n2", "n3", "n4", "n5", "n2"]).SetEdge("n3", "n5").SetEdge("n4", "n2").SetEdge("n4", "n6");
        g.SetPath(["n6", "n7", "n8", "n9", "n6"]).SetEdge("n7", "n9").SetEdge("n8", "n6").SetEdge("n8", "n10");
        var n = g.NodeCount;
        var m = g.EdgeCount;

        // Act
        var fas = DagreAcyclic.GreedyFas(g, _ => 1, Meter());
        foreach (var e in fas)
        {
            g.RemoveEdge(e.V, e.W);
        }

        // Assert
        Assert.Empty(GraphAlgorithms.FindCycles(g));
        Assert.True(fas.Count <= (m / 2) - (n / 6));
    }

    /// <summary>
    ///     Validates that weights steer the greedy choice, and that parallel edges are all returned.
    /// </summary>
    [Fact]
    public void DagreAcyclic_GreedyFas_WeightedAndMultiEdges_ReturnsLightEdges()
    {
        // Arrange
        var g1 = Layout(multigraph: false);
        g1.SetEdge("n1", "n2", new LayoutEdge { Weight = 2 }).SetEdge("n2", "n1", new LayoutEdge { Weight = 1 });
        var g2 = Layout();
        g2.SetEdge("a", "b", new LayoutEdge { Weight = 5 }, "foo");
        g2.SetEdge("b", "a", new LayoutEdge { Weight = 2 }, "bar");
        g2.SetEdge("b", "a", new LayoutEdge { Weight = 2 }, "baz");

        // Act
        var fas1 = DagreAcyclic.GreedyFas(g1, e => g1.Edge(e)!.Weight, Meter());
        var fas2 = DagreAcyclic.GreedyFas(g2, e => g2.Edge(e)!.Weight, Meter());

        // Assert
        Assert.Equal([new GraphEdge("n2", "n1")], fas1);
        Assert.Equal([new GraphEdge("b", "a", "bar"), new GraphEdge("b", "a", "baz")], fas2.OrderBy(e => e.Name, StringComparer.Ordinal));
    }
}
