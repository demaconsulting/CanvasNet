using DemaConsulting.CanvasNet.Mermaid.Dagre;
using DemaConsulting.CanvasNet.Mermaid.Graphlib;
using static DemaConsulting.CanvasNet.Mermaid.Tests.Dagre.DagreTestGraphs;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Dagre;

/// <summary>
///     Unit tests of <see cref="DagreLayout"/>: the layout pipeline and its helpers, with the cases of dagre's own
///     <c>layout.test.js</c> and <c>util.test.js</c>.
/// </summary>
public class DagreLayoutTests
{
    /// <summary>
    ///     Validates that a single node is placed with its corner at the origin.
    /// </summary>
    [Fact]
    public void DagreLayout_Run_SingleNode_PlacedAtHalfSize()
    {
        // Arrange
        var g = Input();
        g.SetNode("a", Node(50, 100));

        // Act
        DagreLayout.Run(g, Meter());

        // Assert
        Assert.Equal((25.0, 50.0), (g.Node("a")!.X, g.Node("a")!.Y));
        Assert.Equal((50.0, 100.0), (g.GraphLabel!.Width, g.GraphLabel.Height));
    }

    /// <summary>
    ///     Validates that two unconnected nodes share a rank, separated by the node separation.
    /// </summary>
    [Fact]
    public void DagreLayout_Run_TwoNodesSameRank_SeparatedByNodeSep()
    {
        // Arrange
        var g = Input(new DagreGraph { NodeSep = 200 });
        g.SetNode("a", Node(50, 100));
        g.SetNode("b", Node(75, 200));

        // Act
        DagreLayout.Run(g, Meter());

        // Assert
        Assert.Equal((25.0, 100.0), (g.Node("a")!.X, g.Node("a")!.Y));
        Assert.Equal((50 + 200 + 37.5, 100.0), (g.Node("b")!.X, g.Node("b")!.Y));
    }

    /// <summary>
    ///     Validates two connected nodes on consecutive ranks, and that an edge without a label gets no label position.
    /// </summary>
    [Fact]
    public void DagreLayout_Run_TwoConnectedNodes_StackedByRankSep()
    {
        // Arrange
        var g = Input(new DagreGraph { RankSep = 300 });
        g.SetNode("a", Node(50, 100));
        g.SetNode("b", Node(75, 200));
        g.SetEdge("a", "b");

        // Act
        DagreLayout.Run(g, Meter());

        // Assert
        Assert.Equal((37.5, 50.0), (g.Node("a")!.X, g.Node("a")!.Y));
        Assert.Equal((37.5, 100 + 300 + 100.0), (g.Node("b")!.X, g.Node("b")!.Y));
        Assert.Null(g.Edge("a", "b")!.X);
        Assert.Null(g.Edge("a", "b")!.Y);
    }

    /// <summary>
    ///     Validates that a labeled edge gets its own rank for the label.
    /// </summary>
    [Fact]
    public void DagreLayout_Run_LabeledEdge_LabelBetweenNodes()
    {
        // Arrange
        var g = Input(new DagreGraph { RankSep = 300 });
        g.SetNode("a", Node(50, 100));
        g.SetNode("b", Node(75, 200));
        g.SetEdge("a", "b", new DagreEdge { Width = 60, Height = 70, LabelPos = "c" });

        // Act
        DagreLayout.Run(g, Meter());

        // Assert
        Assert.Equal((37.5, 50.0), (g.Node("a")!.X, g.Node("a")!.Y));
        Assert.Equal((37.5, 100 + 150 + 70 + 150 + 100.0), (g.Node("b")!.X, g.Node("b")!.Y));
        Assert.Equal((37.5, 100 + 150 + 35.0), (g.Edge("a", "b")!.X, g.Edge("a", "b")!.Y));
    }

    /// <summary>
    ///     Validates that a long label pushes the nodes apart in every rank direction.
    /// </summary>
    /// <param name="rankDir">The rank direction.</param>
    [Theory]
    [InlineData("TB")]
    [InlineData("BT")]
    [InlineData("LR")]
    [InlineData("RL")]
    public void DagreLayout_Run_LongLabel_SeparatesNodes(string rankDir)
    {
        // Arrange
        var g = Input(new DagreGraph { NodeSep = 10, EdgeSep = 10, RankDir = rankDir });
        foreach (var v in new[] { "a", "b", "c", "d" })
        {
            g.SetNode(v, Node(10, 10));
        }

        g.SetEdge("a", "c", new DagreEdge { Width = 2000, Height = 10, LabelPos = "c" });
        g.SetEdge("b", "d", new DagreEdge { Width = 1, Height = 1 });

        // Act
        DagreLayout.Run(g, Meter());

        // Assert
        var distance = rankDir is "TB" or "BT"
            ? Math.Abs(g.Edge("a", "c")!.X!.Value - g.Edge("b", "d")!.X!.Value)
            : Math.Abs(g.Node("a")!.X - g.Node("c")!.X);
        Assert.True(distance > 1000);
    }

    /// <summary>
    ///     Validates that the label offset moves a side label off its edge.
    /// </summary>
    /// <param name="rankDir">The rank direction.</param>
    [Theory]
    [InlineData("TB")]
    [InlineData("BT")]
    [InlineData("LR")]
    [InlineData("RL")]
    public void DagreLayout_Run_LabelOffset_MovesSideLabels(string rankDir)
    {
        // Arrange
        var g = Input(new DagreGraph { NodeSep = 10, EdgeSep = 10, RankDir = rankDir });
        foreach (var v in new[] { "a", "b", "c", "d" })
        {
            g.SetNode(v, Node(10, 10));
        }

        g.SetEdge("a", "b", new DagreEdge { Width = 10, Height = 10, LabelPos = "l", LabelOffset = 1000 });
        g.SetEdge("c", "d", new DagreEdge { Width = 10, Height = 10, LabelPos = "r", LabelOffset = 1000 });

        // Act
        DagreLayout.Run(g, Meter());

        // Assert
        var ab = g.Edge("a", "b")!;
        var cd = g.Edge("c", "d")!;
        if (rankDir is "TB" or "BT")
        {
            Assert.Equal(-1000 - 5.0, ab.X!.Value - ab.Points![0].X);
            Assert.Equal(1000 + 5.0, cd.X!.Value - cd.Points![0].X);
        }
        else
        {
            Assert.Equal(-1000 - 5.0, ab.Y!.Value - ab.Points![0].Y);
            Assert.Equal(1000 + 5.0, cd.Y!.Value - cd.Points![0].Y);
        }
    }

    /// <summary>
    ///     Validates that a short cycle is laid out with one edge pointing down and the other up.
    /// </summary>
    [Fact]
    public void DagreLayout_Run_ShortCycle_OneEdgeUpOneDown()
    {
        // Arrange
        var g = Input(new DagreGraph { RankSep = 200 });
        g.SetNode("a", Node(100, 100));
        g.SetNode("b", Node(100, 100));
        g.SetEdge("a", "b", new DagreEdge { Weight = 2 });
        g.SetEdge("b", "a");

        // Act
        DagreLayout.Run(g, Meter());

        // Assert
        Assert.Equal((50.0, 50.0), (g.Node("a")!.X, g.Node("a")!.Y));
        Assert.Equal((50.0, 100 + 200 + 50.0), (g.Node("b")!.X, g.Node("b")!.Y));
        Assert.True(g.Edge("a", "b")!.Points![1].Y > g.Edge("a", "b")!.Points![0].Y);
        Assert.True(g.Edge("b", "a")!.Points![0].Y > g.Edge("b", "a")!.Points![1].Y);
    }

    /// <summary>
    ///     Validates that routes end on the node borders and pass the dummy positions.
    /// </summary>
    /// <param name="minLen">The minimum length of the edge.</param>
    /// <param name="expectedY">The expected y of every point.</param>
    [Theory]
    [InlineData(1, new[] { 100.0, 200.0, 300.0 })]
    [InlineData(2, new[] { 100.0, 200.0, 300.0, 400.0, 500.0 })]
    public void DagreLayout_Run_EdgeRoute_EndsOnNodeBorders(double minLen, double[] expectedY)
    {
        // Arrange
        var g = Input(new DagreGraph { RankSep = 200 });
        g.SetNode("a", Node(100, 100));
        g.SetNode("b", Node(100, 100));
        g.SetEdge("a", "b", new DagreEdge { MinLen = minLen });

        // Act
        DagreLayout.Run(g, Meter());

        // Assert
        var points = g.Edge("a", "b")!.Points!;
        Assert.Equal(expectedY, points.Select(p => p.Y));
        Assert.All(points, p => Assert.Equal(50.0, p.X));
    }

    /// <summary>
    ///     Validates that a self loop is routed on the side of its node in every rank direction.
    /// </summary>
    /// <param name="rankDir">The rank direction.</param>
    [Theory]
    [InlineData("TB")]
    [InlineData("BT")]
    [InlineData("LR")]
    [InlineData("RL")]
    public void DagreLayout_Run_SelfLoop_RoutedBesideNode(string rankDir)
    {
        // Arrange
        var g = Input(new DagreGraph { EdgeSep = 75, RankDir = rankDir });
        g.SetNode("a", Node(100, 100));
        g.SetEdge("a", "a", new DagreEdge { Width = 50, Height = 50 });

        // Act
        DagreLayout.Run(g, Meter());

        // Assert
        var node = g.Node("a")!;
        var points = g.Edge("a", "a")!.Points!;
        Assert.Equal(7, points.Count);
        Assert.All(points, point =>
        {
            if (rankDir is "TB" or "BT")
            {
                Assert.True(point.X > node.X);
                Assert.True(Math.Abs(point.Y - node.Y) <= node.Height!.Value / 2);
            }
            else
            {
                Assert.True(point.Y > node.Y);
                Assert.True(Math.Abs(point.X - node.X) <= node.Width!.Value / 2);
            }
        });
    }

    /// <summary>
    ///     Validates that the members of a cluster joined only through outside nodes share a rank, and that the cluster
    ///     gets a size around them.
    /// </summary>
    [Fact]
    public void DagreLayout_Run_Cluster_MinimizesHeightAndIsSized()
    {
        // Arrange
        var g = Input();
        foreach (var v in new[] { "a", "b", "c", "d", "x", "y" })
        {
            g.SetNode(v, Node(50, 50));
        }

        g.SetPath(["a", "b", "c", "d"]);
        g.SetEdge("a", "x", new DagreEdge { Weight = 100 });
        g.SetEdge("y", "d", new DagreEdge { Weight = 100 });
        g.SetParent("x", "sg");
        g.SetParent("y", "sg");

        // Act
        DagreLayout.Run(g, Meter());

        // Assert
        Assert.Equal(g.Node("x")!.Y, g.Node("y")!.Y);
        Assert.Null(g.Node("sg"));
    }

    /// <summary>
    ///     Validates that a cluster is sized around its member in every rank direction.
    /// </summary>
    /// <param name="rankDir">The rank direction.</param>
    [Theory]
    [InlineData("tb")]
    [InlineData("bt")]
    [InlineData("lr")]
    [InlineData("rl")]
    public void DagreLayout_Run_ClusterInEveryDirection_SizedAroundMember(string rankDir)
    {
        // Arrange
        var g = Input(new DagreGraph { RankDir = rankDir });
        g.SetNode("a", Node(50, 50));
        g.SetNode("sg", new DagreNode());
        g.SetParent("a", "sg");

        // Act
        DagreLayout.Run(g, Meter());

        // Assert
        var sg = g.Node("sg")!;
        Assert.True(sg.Width > 50 && sg.Height > 50 && sg.X > 25 && sg.Y > 25);
    }

    /// <summary>
    ///     Validates that the coordinates stay in the drawing for a side label, in every rank direction.
    /// </summary>
    /// <param name="rankDir">The rank direction.</param>
    [Theory]
    [InlineData("TB")]
    [InlineData("BT")]
    [InlineData("LR")]
    [InlineData("RL")]
    public void DagreLayout_Run_LeftLabel_StaysInBoundingBox(string rankDir)
    {
        // Arrange
        var g = Input(new DagreGraph { RankDir = rankDir });
        g.SetNode("a", Node(100, 100));
        g.SetNode("b", Node(100, 100));
        g.SetEdge("a", "b", new DagreEdge { Width = 1000, Height = 2000, LabelPos = "l", LabelOffset = 0 });

        // Act
        DagreLayout.Run(g, Meter());

        // Assert
        var edge = g.Edge("a", "b")!;
        if (rankDir is "TB" or "BT")
        {
            Assert.Equal(500.0, edge.X);
        }
        else
        {
            Assert.Equal(1000.0, edge.Y);
        }
    }

    /// <summary>
    ///     Validates that a cluster without its own label still lays out (a member of an implicit parent).
    /// </summary>
    [Fact]
    public void DagreLayout_Run_ImplicitParent_DoesNotFail()
    {
        // Arrange
        var g = Input();
        g.SetNode("a", Node(50, 50));
        g.SetParent("a", "sg1");

        // Act
        DagreLayout.Run(g, Meter());

        // Assert
        Assert.True(double.IsFinite(g.Node("a")!.X) && double.IsFinite(g.Node("a")!.Y));
        Assert.Null(g.Node("sg1"));
    }

    /// <summary>
    ///     Validates that a route that ends at its node's center cannot be cut at the node border, as in dagre.
    /// </summary>
    [Fact]
    public void DagreLayout_IntersectRect_PointAtCenter_Throws()
    {
        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => DagreLayout.IntersectRect(0, 0, 1, 1, new DagrePoint(0, 0)));
    }

    /// <summary>
    ///     Validates that the intersection lies on the line to the center and on the border of the rectangle.
    /// </summary>
    /// <param name="x">The point x.</param>
    /// <param name="y">The point y.</param>
    [Theory]
    [InlineData(2, 6)]
    [InlineData(2, -6)]
    [InlineData(6, 2)]
    [InlineData(-6, 2)]
    [InlineData(5, 0)]
    [InlineData(0, 5)]
    public void DagreLayout_IntersectRect_PointOutside_OnLineAndBorder(double x, double y)
    {
        // Act
        var cross = DagreLayout.IntersectRect(0, 0, 1, 1, new DagrePoint(x, y));

        // Assert
        if (Math.Abs(cross.X - x) > 0)
        {
            var m = (cross.Y - y) / (cross.X - x);
            Assert.Equal(m * cross.X, cross.Y, 12);
        }

        Assert.True(Math.Abs(Math.Abs(cross.X) - 0.5) < 1e-12 || Math.Abs(Math.Abs(cross.Y) - 0.5) < 1e-12);
    }

    /// <summary>
    ///     Validates that the parallel edges of a multigraph collapse into one edge (dagre's <c>simplify</c>).
    /// </summary>
    [Fact]
    public void DagreLayout_Simplify_MultiEdges_Collapsed()
    {
        // Arrange
        var g = Layout();
        g.SetEdge("a", "b", new LayoutEdge { Weight = 1, MinLen = 1 });
        g.SetEdge("a", "b", new LayoutEdge { Weight = 2, MinLen = 2 }, "multi");

        // Act
        var simple = DagreLayout.Simplify(g);

        // Assert
        Assert.False(simple.IsMultigraph);
        Assert.Equal(1, simple.EdgeCount);
        Assert.Equal((3.0, 2.0), (simple.Edge("a", "b")!.Weight, simple.Edge("a", "b")!.MinLen));
        Assert.Same(g.GraphLabel, simple.GraphLabel);
    }

    /// <summary>
    ///     Validates that the flat copy drops the clusters and keeps every edge.
    /// </summary>
    [Fact]
    public void DagreLayout_AsNonCompoundGraph_Cluster_Dropped()
    {
        // Arrange
        var g = Layout(compound: true);
        g.SetParent("a", "sg1");
        g.SetEdge("a", "b", new LayoutEdge(), "multi");

        // Act
        var flat = DagreLayout.AsNonCompoundGraph(g);

        // Assert
        Assert.False(flat.IsCompound);
        Assert.Equal(["a", "b"], flat.Nodes());
        Assert.True(flat.HasEdge("a", "b", "multi"));
    }

    /// <summary>
    ///     Validates the layer matrix built from ranks and orders.
    /// </summary>
    [Fact]
    public void DagreLayout_BuildLayerMatrix_RanksAndOrders_Matrix()
    {
        // Arrange
        var g = Layout();
        g.SetNode("a", Ranked(0, 0)).SetNode("b", Ranked(0, 1)).SetNode("c", Ranked(1, 0)).SetNode("d", Ranked(1, 1)).SetNode("e", Ranked(2, 0));

        // Act
        var layers = DagreLayout.BuildLayerMatrix(g);

        // Assert
        Assert.Equal(3, layers.Count);
        Assert.Equal(["a", "b"], layers[0]);
        Assert.Equal(["c", "d"], layers[1]);
        Assert.Equal(["e"], layers[2]);
    }

    /// <summary>
    ///     Validates that ranks are shifted to start at zero, and that a cluster gets no rank.
    /// </summary>
    [Fact]
    public void DagreLayout_NormalizeRanks_NegativeAndCluster_ShiftedToZero()
    {
        // Arrange
        var g = Layout(compound: true);
        g.SetNode("a", Ranked(-3)).SetNode("b", Ranked(-2)).SetNode("sg", new LayoutNode()).SetParent("a", "sg");

        // Act
        DagreLayout.NormalizeRanks(g);

        // Assert
        Assert.Equal((0.0, 1.0), (g.Node("a")!.Rank, g.Node("b")!.Rank));
        Assert.Null(g.Node("sg")!.Rank);
    }

    /// <summary>
    ///     Validates that empty border ranks are removed and others kept.
    /// </summary>
    /// <param name="rank">The rank of the second node.</param>
    /// <param name="expected">Its rank afterwards.</param>
    [Theory]
    [InlineData(4, 1)]
    [InlineData(8, 2)]
    public void DagreLayout_RemoveEmptyRanks_BorderRanks_Removed(double rank, double expected)
    {
        // Arrange
        var g = Layout();
        g.GraphLabel!.NodeRankFactor = 4;
        g.SetNode("a", Ranked(0)).SetNode("b", Ranked(rank));

        // Act
        DagreLayout.RemoveEmptyRanks(g);

        // Assert
        Assert.Equal(0.0, g.Node("a")!.Rank);
        Assert.Equal(expected, g.Node("b")!.Rank);
    }

    /// <summary>
    ///     Validates that the input labels of a laid-out graph receive the results and that a graph laid out twice gets
    ///     the same results.
    /// </summary>
    [Fact]
    public void DagreLayout_Run_Twice_SameResults()
    {
        // Arrange
        var g = Input(new DagreGraph { RankDir = "LR" });
        g.SetEdge("1", "2", new DagreEdge { Width = 20, Height = 10 }, "e1");
        g.SetEdge("2", "10", new DagreEdge(), "e2");
        g.SetEdge("10", "1", new DagreEdge(), "e3");
        foreach (var v in g.Nodes())
        {
            g.SetNode(v, Node(30, 20));
        }

        // Act
        DagreLayout.Run(g, Meter());
        var first = g.Edges().Select(e => string.Join(";", g.Edge(e)!.Points!.Select(p => $"{p.X:R},{p.Y:R}"))).ToList();
        DagreLayout.Run(g, Meter());
        var second = g.Edges().Select(e => string.Join(";", g.Edge(e)!.Points!.Select(p => $"{p.X:R},{p.Y:R}"))).ToList();

        // Assert
        Assert.Equal(first, second);
        Assert.Equal([new GraphEdge("1", "2", "e1"), new GraphEdge("2", "10", "e2"), new GraphEdge("10", "1", "e3")], g.Edges());
    }
}
