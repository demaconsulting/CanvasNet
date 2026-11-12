using DemaConsulting.CanvasNet.Mermaid.Dagre;
using DemaConsulting.CanvasNet.Mermaid.Graphlib;
using static DemaConsulting.CanvasNet.Mermaid.Tests.Dagre.DagreTestGraphs;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Dagre;

/// <summary>
///     Unit tests of <see cref="DagreNesting"/>, with the cases of dagre's <c>nesting-graph.test.js</c>,
///     <c>parent-dummy-chains.test.js</c> and <c>add-border-segments.test.js</c>.
/// </summary>
public class DagreNestingTests
{
    /// <summary>
    ///     Validates that a flat graph gets a root joined to every node, multiplied edge lengths of one, and a rank
    ///     factor of one.
    /// </summary>
    [Fact]
    public void DagreNesting_Run_FlatGraph_ConnectsRoot()
    {
        // Arrange
        var g = Layout(compound: true);
        g.SetEdge("a", "b", new LayoutEdge { MinLen = 2, Weight = 1 }).SetNode("c");

        // Act
        DagreNesting.Run(g, Meter());

        // Assert
        var root = g.GraphLabel!.NestingRoot!;
        Assert.Equal(1.0, g.GraphLabel.NodeRankFactor);
        Assert.Equal("root", g.Node(root)!.Dummy);
        Assert.Equal(["a", "b", "c"], g.Successors(root)!);
        Assert.All(g.OutEdges(root)!, e => Assert.Equal((0.0, 1.0), (g.Edge(e)!.Weight, g.Edge(e)!.MinLen)));
        Assert.Equal(2.0, g.Edge("a", "b")!.MinLen);
    }

    /// <summary>
    ///     Validates that a cluster gets top and bottom borders around its members and that edge lengths are
    ///     multiplied by the rank factor.
    /// </summary>
    [Fact]
    public void DagreNesting_Run_Cluster_AddsBorders()
    {
        // Arrange
        var g = Layout(compound: true);
        g.SetParent("a", "sg").SetEdge("a", "b");

        // Act
        DagreNesting.Run(g, Meter());

        // Assert
        var sg = g.Node("sg")!;
        Assert.Equal(3.0, g.GraphLabel!.NodeRankFactor);
        Assert.Equal(3.0, g.Edge("a", "b")!.MinLen);
        Assert.Equal("sg", g.Parent(sg.BorderTop!));
        Assert.Equal("sg", g.Parent(sg.BorderBottom!));
        Assert.True(g.Edge(sg.BorderTop!, "a")!.NestingEdge);
        Assert.True(g.Edge("a", sg.BorderBottom!)!.NestingEdge);
        Assert.True(g.HasEdge(g.GraphLabel.NestingRoot!, sg.BorderTop!));
    }

    /// <summary>
    ///     Validates that the clean-up removes the root and the nesting edges and keeps the other edges.
    /// </summary>
    [Fact]
    public void DagreNesting_Cleanup_AfterRun_RemovesNestingGraph()
    {
        // Arrange
        var g = Layout(compound: true);
        g.SetParent("a", "sg").SetEdge("a", "b");
        DagreNesting.Run(g, Meter());

        // Act
        DagreNesting.Cleanup(g);

        // Assert
        Assert.Null(g.GraphLabel!.NestingRoot);
        Assert.Equal([new GraphEdge("a", "b")], g.Edges());
    }

    /// <summary>
    ///     Validates that the dummy nodes of an edge leaving a cluster are moved into the cluster while it spans their
    ///     rank.
    /// </summary>
    [Fact]
    public void DagreNesting_ParentDummyChains_EdgeLeavingCluster_DummiesFollowCluster()
    {
        // Arrange
        var g = Layout(compound: true);
        g.SetParent("a", "sg1");
        g.SetNode("sg1", new LayoutNode { MinRank = 0, MaxRank = 2 });
        g.SetNode("a", Ranked(0)).SetNode("b", Ranked(4));
        g.SetNode("d1", new LayoutNode { Rank = 1, EdgeObj = new GraphEdge("a", "b"), Dummy = "edge" });
        g.SetNode("d2", new LayoutNode { Rank = 2, EdgeObj = new GraphEdge("a", "b"), Dummy = "edge" });
        g.SetNode("d3", new LayoutNode { Rank = 3, EdgeObj = new GraphEdge("a", "b"), Dummy = "edge" });
        g.SetPath(["a", "d1", "d2", "d3", "b"]);
        g.GraphLabel!.DummyChains = ["d1"];

        // Act
        DagreNesting.ParentDummyChains(g, Meter());

        // Assert
        Assert.Equal(("sg1", "sg1", (string?)null), (g.Parent("d1"), g.Parent("d2"), g.Parent("d3")));
    }

    /// <summary>
    ///     Validates that a cluster gets a left and a right border node at each of its ranks, joined from rank to rank.
    /// </summary>
    [Fact]
    public void DagreNesting_AddBorderSegments_ClusterOverTwoRanks_AddsChains()
    {
        // Arrange
        var g = Layout(compound: true);
        g.SetParent("a", "sg");
        g.SetNode("sg", new LayoutNode { MinRank = 1, MaxRank = 2 });

        // Act
        DagreNesting.AddBorderSegments(g, Meter());

        // Assert
        var sg = g.Node("sg")!;
        Assert.Equal(3, sg.BorderLeft!.Count);
        Assert.Null(sg.BorderLeft[0]);
        var (l1, l2) = (sg.BorderLeft[1]!, sg.BorderLeft[2]!);
        Assert.Equal(("border", "borderLeft", 1.0), (g.Node(l1)!.Dummy, g.Node(l1)!.BorderType, g.Node(l1)!.Rank));
        Assert.Equal("sg", g.Parent(l1));
        Assert.True(g.HasEdge(l1, l2));
        Assert.True(g.HasEdge(sg.BorderRight![1]!, sg.BorderRight[2]!));
        Assert.Null(g.Node("a")!.BorderLeft);
    }
}
