using DemaConsulting.CanvasNet.Mermaid.Dagre;
using static DemaConsulting.CanvasNet.Mermaid.Tests.Dagre.DagreTestGraphs;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Dagre;

/// <summary>
///     Unit tests of <see cref="DagrePositioning"/>, with the cases of dagre's <c>bk.test.js</c> and
///     <c>position.test.js</c>.
/// </summary>
public class DagrePositioningTests
{
    /// <summary>
    ///     Validates the type-1 conflicts of a crossing: none without dummies or with only dummies, and the non-inner
    ///     segment marked when the other is an inner segment.
    /// </summary>
    /// <param name="dummies">The dummy nodes.</param>
    /// <param name="adConflict">Whether a-d is marked.</param>
    /// <param name="bcConflict">Whether b-c is marked.</param>
    [Theory]
    [InlineData("", false, false)]
    [InlineData("a", false, false)]
    [InlineData("b c d", true, false)]
    [InlineData("a c d", false, true)]
    [InlineData("a b d", false, true)]
    [InlineData("a b c", true, false)]
    [InlineData("a b c d", false, false)]
    public void DagrePositioning_FindType1Conflicts_Crossing_MarksNonInnerSegment(string dummies, bool adConflict, bool bcConflict)
    {
        // Arrange
        var g = Layout(multigraph: false);
        g.SetNode("a", Ranked(0, 0)).SetNode("b", Ranked(0, 1)).SetNode("c", Ranked(1, 0)).SetNode("d", Ranked(1, 1));
        g.SetEdge("a", "d").SetEdge("b", "c");
        foreach (var v in dummies.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            g.Node(v)!.Dummy = "edge";
        }

        // Act
        var conflicts = DagrePositioning.FindType1Conflicts(g, DagreLayout.BuildLayerMatrix(g), Meter());

        // Assert
        Assert.Equal(adConflict, DagrePositioning.HasConflict(conflicts, "a", "d"));
        Assert.Equal(adConflict, DagrePositioning.HasConflict(conflicts, "d", "a"));
        Assert.Equal(bcConflict, DagrePositioning.HasConflict(conflicts, "b", "c"));
    }

    /// <summary>
    ///     Validates the type-2 conflicts: an inner segment that crosses a border segment is marked.
    /// </summary>
    [Fact]
    public void DagrePositioning_FindType2Conflicts_InnerCrossesBorder_Marked()
    {
        // Arrange
        var g = Layout(multigraph: false);
        g.SetNode("a", new LayoutNode { Rank = 0, Order = 0, Dummy = "border" });
        g.SetNode("b", new LayoutNode { Rank = 0, Order = 1, Dummy = "edge" });
        g.SetNode("c", new LayoutNode { Rank = 1, Order = 0, Dummy = "edge" });
        g.SetNode("d", new LayoutNode { Rank = 1, Order = 1, Dummy = "border" });
        g.SetEdge("a", "d").SetEdge("b", "c");

        // Act
        var conflicts = DagrePositioning.FindType2Conflicts(g, DagreLayout.BuildLayerMatrix(g), Meter());

        // Assert
        Assert.False(DagrePositioning.HasConflict(conflicts, "a", "d"));
        Assert.True(DagrePositioning.HasConflict(conflicts, "b", "c"));
    }

    /// <summary>
    ///     Validates the vertical alignment with medians: the left median when possible, the right when the left
    ///     conflicts, and the order of the neighbors by position rather than by name.
    /// </summary>
    [Fact]
    public void DagrePositioning_VerticalAlignment_Medians_AlignsBlocks()
    {
        // Arrange
        var g = Layout(multigraph: false);
        g.SetNode("b", Ranked(0, 1)).SetNode("c", Ranked(1, 0)).SetNode("z", Ranked(0, 0));
        g.SetEdge("z", "c").SetEdge("b", "c");
        var layering = DagreLayout.BuildLayerMatrix(g);
        var conflicts = new HashSet<(string, string)>();

        // Act
        var (root, align) = DagrePositioning.VerticalAlignment(layering, conflicts, v => g.Predecessors(v)!);
        DagrePositioning.AddConflict(conflicts, "z", "c");
        var (root2, align2) = DagrePositioning.VerticalAlignment(layering, conflicts, v => g.Predecessors(v)!);

        // Assert
        Assert.Equal(("z", "b", "z"), (root["z"], root["b"], root["c"]));
        Assert.Equal(("c", "b", "z"), (align["z"], align["b"], align["c"]));
        Assert.Equal(("z", "b", "b"), (root2["z"], root2["b"], root2["c"]));
        Assert.Equal(("z", "c", "b"), (align2["z"], align2["b"], align2["c"]));
    }

    /// <summary>
    ///     Validates the balance of the four alignments: the average of the two medians.
    /// </summary>
    [Fact]
    public void DagrePositioning_Balance_FourAlignments_AveragesMedians()
    {
        // Arrange
        var xss = new List<(string Name, Dictionary<string, double> Xs)>
        {
            ("ul", new Dictionary<string, double> { ["a"] = 0, ["b"] = 50 }),
            ("ur", new Dictionary<string, double> { ["a"] = 75, ["b"] = 0 }),
            ("dl", new Dictionary<string, double> { ["a"] = 125, ["b"] = 60 }),
            ("dr", new Dictionary<string, double> { ["a"] = 200, ["b"] = 75 }),
        };

        // Act
        var balanced = DagrePositioning.Balance(xss, null);
        var chosen = DagrePositioning.Balance(xss, "DL");

        // Assert
        Assert.Equal((100.0, 55.0), (balanced["a"], balanced["b"]));
        Assert.Equal((125.0, 60.0), (chosen["a"], chosen["b"]));
        Assert.Throws<InvalidOperationException>(() => DagrePositioning.Balance(xss, "XX"));
    }

    /// <summary>
    ///     Validates that the left alignments are shifted to the reference's left edge and the right ones to its right
    ///     edge.
    /// </summary>
    [Fact]
    public void DagrePositioning_AlignCoordinates_ToReference_ShiftsEdges()
    {
        // Arrange
        var reference = new Dictionary<string, double> { ["a"] = 50, ["b"] = 75 };
        var xss = new List<(string Name, Dictionary<string, double> Xs)>
        {
            ("ul", reference),
            ("ur", new Dictionary<string, double> { ["a"] = 0, ["b"] = 25 }),
            ("dl", new Dictionary<string, double> { ["a"] = 0, ["b"] = 25 }),
            ("dr", new Dictionary<string, double> { ["a"] = 100, ["b"] = 125 }),
        };

        // Act
        DagrePositioning.AlignCoordinates(xss, reference);

        // Assert
        Assert.Same(reference, xss[0].Xs);
        Assert.Equal((50.0, 75.0), (xss[1].Xs["a"], xss[1].Xs["b"]));
        Assert.Equal((50.0, 75.0), (xss[2].Xs["a"], xss[2].Xs["b"]));
        Assert.Equal((50.0, 75.0), (xss[3].Xs["a"], xss[3].Xs["b"]));
    }

    /// <summary>
    ///     Validates the narrowest alignment, node widths included.
    /// </summary>
    [Fact]
    public void DagrePositioning_FindSmallestWidthAlignment_Widths_PicksNarrowest()
    {
        // Arrange
        var g = Layout(multigraph: false);
        g.SetNode("a", new LayoutNode { Width = 50 }).SetNode("b", new LayoutNode { Width = 50 });
        var wide = new Dictionary<string, double> { ["a"] = 0, ["b"] = 1000 };
        var narrow = new Dictionary<string, double> { ["a"] = 0, ["b"] = 75 };

        // Act
        var smallest = DagrePositioning.FindSmallestWidthAlignment(g, [wide, narrow, wide, narrow]);

        // Assert
        Assert.Same(narrow, smallest);
    }

    /// <summary>
    ///     Validates the x positions of small graphs.
    /// </summary>
    [Fact]
    public void DagrePositioning_PositionX_SmallGraphs_MatchDagre()
    {
        // Arrange
        var single = Layout(multigraph: false);
        single.SetNode("a", new LayoutNode { Rank = 0, Order = 0, Width = 100 });
        var fork = Layout(multigraph: false);
        fork.GraphLabel!.NodeSep = 10;
        fork.SetNode("a", new LayoutNode { Rank = 0, Order = 0, Width = 20 });
        fork.SetNode("b", new LayoutNode { Rank = 1, Order = 0, Width = 50 });
        fork.SetNode("c", new LayoutNode { Rank = 1, Order = 1, Width = 50 });
        fork.SetEdge("a", "b").SetEdge("a", "c");
        var shifted = Layout(multigraph: false);
        shifted.GraphLabel!.NodeSep = 10;
        shifted.SetNode("a", new LayoutNode { Rank = 0, Order = 0, Width = 50 });
        shifted.SetNode("b", new LayoutNode { Rank = 0, Order = 1, Width = 60 });
        shifted.SetNode("c", new LayoutNode { Rank = 1, Order = 0, Width = 70 });
        shifted.SetNode("d", new LayoutNode { Rank = 1, Order = 1, Width = 80 });
        shifted.SetEdge("b", "c");

        // Act
        var x1 = DagrePositioning.PositionX(single, Meter());
        var x2 = DagrePositioning.PositionX(fork, Meter());
        var x3 = DagrePositioning.PositionX(shifted, Meter());

        // Assert
        Assert.Equal(0.0, x1["a"]);
        Assert.Equal((x2["a"] - 30, x2["a"] + 30), (x2["b"], x2["c"]));
        Assert.Equal(x3["b"], x3["c"]);
        Assert.Equal(x3["b"] - 30 - 10 - 25, x3["a"]);
        Assert.Equal(x3["c"] + 35 + 10 + 40, x3["d"]);
    }

    /// <summary>
    ///     Validates the y positions: each rank as tall as its tallest node, separated by the rank separation.
    /// </summary>
    [Fact]
    public void DagrePositioning_Position_Ranks_StackedByTallestNode()
    {
        // Arrange
        var g = Layout(compound: true);
        g.GraphLabel!.RankSep = 1000;
        g.SetNode("a", new LayoutNode { Rank = 0, Order = 0, Width = 10, Height = 100 });
        g.SetNode("b", new LayoutNode { Rank = 0, Order = 1, Width = 10, Height = 80 });
        g.SetNode("c", new LayoutNode { Rank = 1, Order = 0, Width = 10, Height = 50 });

        // Act
        DagrePositioning.Position(g, Meter());

        // Assert
        Assert.Equal((50.0, 50.0, 100 + 1000 + 25.0), (g.Node("a")!.Y, g.Node("b")!.Y, g.Node("c")!.Y));
    }
}
