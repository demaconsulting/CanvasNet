using DemaConsulting.CanvasNet.Mermaid.Dagre;
using DemaConsulting.CanvasNet.Mermaid.Graphlib;
using static DemaConsulting.CanvasNet.Mermaid.Tests.Dagre.DagreTestGraphs;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Dagre;

/// <summary>
///     Unit tests of <see cref="DagreRanker"/>, with the cases of dagre's <c>network-simplex.test.js</c>,
///     <c>feasible-tree.test.js</c> and <c>rank/util.test.js</c>.
/// </summary>
public class DagreRankerTests
{
    /// <summary>
    ///     Validates the network simplex ranks of small graphs (normalized to start at zero, as dagre's tests do).
    /// </summary>
    /// <param name="paths">The paths of the graph, each a space-separated list of nodes.</param>
    /// <param name="expected">The expected ranks, as <c>node=rank</c> pairs.</param>
    [Theory]
    [InlineData(new[] { "a" }, "a=0")]
    [InlineData(new[] { "a b" }, "a=0 b=1")]
    [InlineData(new[] { "a b d", "a c d" }, "a=0 b=1 c=1 d=2")]
    [InlineData(new[] { "a b c d h", "a e g h", "a f g" }, "a=0 b=1 c=2 d=3 h=4 e=1 f=1 g=2")]
    public void DagreRanker_NetworkSimplex_SmallGraphs_OptimalRanks(string[] paths, string expected)
    {
        // Arrange
        var g = Layout();
        foreach (var path in paths)
        {
            var nodes = path.Split(' ');
            if (nodes.Length == 1)
            {
                g.SetNode(nodes[0]);
            }

            g.SetPath(nodes);
        }

        // Act
        DagreRanker.NetworkSimplex(g, Meter());
        DagreLayout.NormalizeRanks(g);

        // Assert
        Assert.Equal(expected, string.Join(' ', expected.Split(' ').Select(pair => pair.Split('=')[0]).Select(v => $"{v}={g.Node(v)!.Rank}")));
    }

    /// <summary>
    ///     Validates that the minimum length is honored.
    /// </summary>
    [Fact]
    public void DagreRanker_NetworkSimplex_MinLen_Honored()
    {
        // Arrange
        var g = Layout();
        g.SetPath(["a", "b", "d"]).SetEdge("a", "c").SetEdge("c", "d", new LayoutEdge { MinLen = 2, Weight = 1 });

        // Act
        DagreRanker.NetworkSimplex(g, Meter());
        DagreLayout.NormalizeRanks(g);

        // Assert
        Assert.Equal([0.0, 2.0, 1.0, 3.0], new[] { "a", "b", "c", "d" }.Select(v => g.Node(v)!.Rank!.Value));
    }

    /// <summary>
    ///     Validates that parallel edges take the greatest minimum length.
    /// </summary>
    [Fact]
    public void DagreRanker_NetworkSimplex_MultiEdges_GreatestMinLen()
    {
        // Arrange
        var g = Layout();
        g.SetPath(["a", "b", "c", "d"]);
        g.SetEdge("a", "e", new LayoutEdge { Weight = 2, MinLen = 1 });
        g.SetEdge("e", "d");
        g.SetEdge("b", "c", new LayoutEdge { Weight = 1, MinLen = 2 }, "multi");

        // Act
        DagreRanker.NetworkSimplex(g, Meter());
        DagreLayout.NormalizeRanks(g);

        // Assert
        Assert.Equal([0.0, 1.0, 3.0, 4.0, 1.0], new[] { "a", "b", "c", "d", "e" }.Select(v => g.Node(v)!.Rank!.Value));
    }

    /// <summary>
    ///     Validates that no edge leaves when every cut value is non-negative, and the first negative one leaves.
    /// </summary>
    [Fact]
    public void DagreRanker_LeaveEdge_NegativeCutValue_Found()
    {
        // Arrange
        var positive = new Graph<object, DagreRanker.TreeNode, DagreRanker.TreeEdge>(directed: false);
        positive.SetEdge("a", "b", new DagreRanker.TreeEdge { CutValue = 1 }).SetEdge("b", "c", new DagreRanker.TreeEdge { CutValue = 1 });
        var negative = new Graph<object, DagreRanker.TreeNode, DagreRanker.TreeEdge>(directed: false);
        negative.SetEdge("a", "b", new DagreRanker.TreeEdge { CutValue = 1 }).SetEdge("b", "c", new DagreRanker.TreeEdge { CutValue = -1 });

        // Act / Assert
        Assert.Null(DagreRanker.LeaveEdge(positive, Meter()));
        Assert.Equal(new GraphEdge("b", "c"), DagreRanker.LeaveEdge(negative, Meter()));
    }

    /// <summary>
    ///     Validates that the entering edge crosses from the head to the tail component, whichever side the root is on.
    /// </summary>
    /// <param name="root">The root of the tree numbering.</param>
    [Theory]
    [InlineData("c")]
    [InlineData("b")]
    public void DagreRanker_EnterEdge_HeadToTail_Found(string root)
    {
        // Arrange
        var g = Layout();
        g.SetNode("a", Ranked(0)).SetNode("b", Ranked(2)).SetNode("c", Ranked(3)).SetPath(["a", "b", "c"]).SetEdge("a", "c");
        var t = new Graph<object, DagreRanker.TreeNode, DagreRanker.TreeEdge>(directed: false)
            .SetDefaultNodeLabel(_ => new DagreRanker.TreeNode())
            .SetDefaultEdgeLabel((_, _, _) => new DagreRanker.TreeEdge());
        t.SetPath(["b", "c", "a"]);
        DagreRanker.InitLowLimValues(t, root);

        // Act
        var f = DagreRanker.EnterEdge(t, g, new GraphEdge("b", "c"), Meter());

        // Assert
        Assert.Equal(new GraphEdge("a", "b"), f);
    }

    /// <summary>
    ///     Validates the postorder numbering and parents of a tree.
    /// </summary>
    [Fact]
    public void DagreRanker_InitLowLimValues_Tree_NumbersInPostorder()
    {
        // Arrange
        var t = new Graph<object, DagreRanker.TreeNode, DagreRanker.TreeEdge>(directed: false)
            .SetDefaultNodeLabel(_ => new DagreRanker.TreeNode())
            .SetDefaultEdgeLabel((_, _, _) => new DagreRanker.TreeEdge());
        t.SetNode("a").SetEdge("a", "b").SetEdge("a", "c").SetEdge("b", "d").SetEdge("b", "e");

        // Act
        DagreRanker.InitLowLimValues(t);

        // Assert
        var a = t.Node("a")!;
        Assert.Equal((1, 5, (string?)null), (a.Low, a.Lim, a.Parent));
        Assert.Equal((1, 3, "a"), (t.Node("b")!.Low, t.Node("b")!.Lim, t.Node("b")!.Parent));
        Assert.Equal((1, 1, "b"), (t.Node("d")!.Low, t.Node("d")!.Lim, t.Node("d")!.Parent));
        Assert.Equal((2, 2, "b"), (t.Node("e")!.Low, t.Node("e")!.Lim, t.Node("e")!.Parent));
        Assert.Equal((4, 4, "a"), (t.Node("c")!.Low, t.Node("c")!.Lim, t.Node("c")!.Parent));
    }

    /// <summary>
    ///     Validates the cut value of the Gansner example's tree edge g-h.
    /// </summary>
    [Fact]
    public void DagreRanker_CalcCutValue_GansnerTree_MatchesPaper()
    {
        // Arrange
        var g = Layout(multigraph: false);
        g.SetPath(["a", "b", "c", "d", "h"]).SetPath(["a", "e", "g", "h"]).SetPath(["a", "f", "g"]);
        var t = new Graph<object, DagreRanker.TreeNode, DagreRanker.TreeEdge>(directed: false)
            .SetDefaultNodeLabel(_ => new DagreRanker.TreeNode())
            .SetDefaultEdgeLabel((_, _, _) => new DagreRanker.TreeEdge());
        t.SetPath(["a", "b", "c", "d", "h", "g", "e"]).SetEdge("g", "f");
        DagreRanker.InitLowLimValues(t);

        // Act
        DagreRanker.InitCutValues(t, g, Meter());

        // Assert
        Assert.Equal(3.0, t.Edge("a", "b")!.CutValue);
        Assert.Equal(3.0, t.Edge("b", "c")!.CutValue);
        Assert.Equal(3.0, t.Edge("c", "d")!.CutValue);
        Assert.Equal(3.0, t.Edge("d", "h")!.CutValue);
        Assert.Equal(-1.0, t.Edge("g", "h")!.CutValue);
        Assert.Equal(0.0, t.Edge("e", "g")!.CutValue);
        Assert.Equal(0.0, t.Edge("f", "g")!.CutValue);
    }

    /// <summary>
    ///     Validates the longest-path ranking and the slack.
    /// </summary>
    [Fact]
    public void DagreRanker_LongestPath_Chain_RanksBySinkDistance()
    {
        // Arrange
        var g = Layout();
        g.SetPath(["a", "b", "c"]).SetEdge("a", "c", new LayoutEdge { MinLen = 3, Weight = 1 });

        // Act
        DagreRanker.LongestPath(g, Meter());

        // Assert
        Assert.Equal([-3.0, -1.0, 0.0], new[] { "a", "b", "c" }.Select(v => g.Node(v)!.Rank!.Value));
        Assert.Equal(1.0, DagreRanker.Slack(g, new GraphEdge("a", "b")));
        Assert.Equal(0.0, DagreRanker.Slack(g, new GraphEdge("a", "c")));
    }

    /// <summary>
    ///     Validates that a node without a label (the end of an edge at a subgraph, which the flat graph leaves out) fails
    ///     the longest-path ranking as dagre's does, with an exception the layout reports as a located failure.
    /// </summary>
    [Fact]
    public void DagreRanker_LongestPath_UnlabeledNode_Throws()
    {
        // Arrange: without a default node label, graphlib's setEdge creates the missing end without a label, as in
        // dagre's flat copy of a graph whose edge ends at a subgraph
        var g = new Graph<LayoutGraphLabel, LayoutNode, LayoutEdge>(multigraph: true, compound: false).SetGraph(new LayoutGraphLabel());
        g.SetNode("a", new LayoutNode());
        g.SetEdge("a", "cluster", new LayoutEdge { MinLen = 1, Weight = 1 });

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => DagreRanker.LongestPath(g, Meter()));

        // Assert
        Assert.Contains("'cluster'", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the feasible tree is tight and spans the graph.
    /// </summary>
    [Fact]
    public void DagreRanker_FeasibleTree_ShortensLooseEdges_SpansGraph()
    {
        // Arrange
        var g = Layout();
        g.SetNode("a", Ranked(0)).SetNode("b", Ranked(1)).SetNode("c", Ranked(3)).SetNode("d", Ranked(4));
        g.SetPath(["a", "b", "c", "d"]).SetEdge("a", "d", new LayoutEdge { MinLen = 4, Weight = 1 });

        // Act
        var t = DagreRanker.FeasibleTree(g, Meter());

        // Assert
        Assert.Equal(4, t.NodeCount);
        Assert.Equal(3, t.EdgeCount);
        Assert.All(t.Edges(), e => Assert.Equal(0.0, DagreRanker.Slack(g, g.HasEdge(e.V, e.W) ? e : Reversed(e))));
    }

    /// <summary>
    ///     Validates the ranker choice by the graph's attribute.
    /// </summary>
    /// <param name="ranker">The ranker.</param>
    /// <param name="expectedB">The expected rank of the side node after normalization.</param>
    [Theory]
    [InlineData("network-simplex", 1)]
    [InlineData("tight-tree", 1)]
    [InlineData("longest-path", 2)]
    [InlineData(null, 1)]
    public void DagreRanker_Rank_RankerAttribute_PicksRanker(string? ranker, double expectedB)
    {
        // Arrange
        var g = Layout();
        g.GraphLabel!.Ranker = ranker;
        g.SetPath(["a", "c", "d"]).SetPath(["a", "b"]).SetEdge("x", "b");

        // Act
        DagreRanker.Rank(g, Meter());
        DagreLayout.NormalizeRanks(g);

        // Assert
        Assert.Equal(expectedB, g.Node("b")!.Rank);
    }

    /// <summary>
    ///     Gets an edge with its ends swapped.
    /// </summary>
    /// <param name="e">The edge.</param>
    /// <returns>The reversed edge.</returns>
    private static GraphEdge Reversed(GraphEdge e)
    {
        var (tail, head) = (e.W, e.V);
        return new GraphEdge(tail, head);
    }
}
