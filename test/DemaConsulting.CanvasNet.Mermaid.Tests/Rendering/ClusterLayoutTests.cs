using DemaConsulting.CanvasNet.Mermaid.Dagre;
using DemaConsulting.CanvasNet.Mermaid.Graphlib;
using DemaConsulting.CanvasNet.Mermaid.Rendering;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Rendering;

/// <summary>
///     Tests of <see cref="ClusterLayout"/>: subgraphs laid out on their own, links moved to anchors and cut at
///     subgraph borders, self-loops, subgraph titles, the drawing order and the work budget.
/// </summary>
public sealed class ClusterLayoutTests
{
    /// <summary>
    ///     Checks that a subgraph without links to the outside is laid out on its own, in the other direction of the
    ///     diagram's, and drawn as a group that stands in for its contents.
    /// </summary>
    [Fact]
    public void ClusterLayout_Render_IsolatedSubgraph_IsLaidOutOnItsOwnInTheOtherDirection()
    {
        // Act
        var graph = RenderingTestContext.Render("flowchart TB\n  subgraph S[Isolated]\n    a1 --> a2\n  end\n  x --> y\n  y --> S\n");

        // Assert
        var scene = RenderingTestContext.SceneOf(graph);
        var subgraph = Assert.Single(SceneQuery.OfRole(scene, "subgraph"));
        Assert.Equal("S", subgraph.Id);
        var nodes = SceneQuery.Nodes(scene);
        Assert.Equal(nodes["flowchart-a1-0"].Y, nodes["flowchart-a2-1"].Y, 9);
        Assert.True(nodes["flowchart-a2-1"].X > nodes["flowchart-a1-0"].X);
        Assert.True(SceneQuery.Clusters(scene)["S"].Encloses(nodes["flowchart-a1-0"].Box));
    }

    /// <summary>
    ///     Checks that a direction declared in an isolated subgraph wins over the flipped one.
    /// </summary>
    [Fact]
    public void ClusterLayout_Render_DeclaredDirection_Wins()
    {
        // Act
        var graph = RenderingTestContext.Render("flowchart TB\n  subgraph S\n    direction RL\n    a --> b\n  end\n");

        // Assert
        var nodes = SceneQuery.Nodes(RenderingTestContext.SceneOf(graph));
        Assert.True(nodes["flowchart-b-1"].X < nodes["flowchart-a-0"].X);
        Assert.Equal(nodes["flowchart-a-0"].Y, nodes["flowchart-b-1"].Y, 9);
    }

    /// <summary>
    ///     Checks that links to a subgraph without links to the outside end on the box of the laid-out subgraph, which
    ///     stands in for it: the line 4 pixels short for the arrowhead, the start on the box.
    /// </summary>
    [Fact]
    public void ClusterLayout_Render_LinksToIsolatedSubgraph_EndOnItsBox()
    {
        // Act
        var graph = RenderingTestContext.Render("flowchart LR\n  subgraph S1\n    a --> b\n  end\n  S1 --> c\n  d --> S1\n");

        // Assert
        var scene = RenderingTestContext.SceneOf(graph);
        var cluster = SceneQuery.Clusters(scene)["S1"];
        var edges = SceneQuery.Edges(scene);
        Assert.Equal(cluster.X - 4, edges["L_d_S1_0"].Segments[^1].X, 0.002);
        Assert.Equal(cluster.Right, edges["L_S1_c_0"].Segments[0].X, 0.002);
        Assert.Equal("S1", Assert.Single(SceneQuery.OfRole(scene, "subgraph")).Id);
    }

    /// <summary>
    ///     Checks that a link from a subgraph with links to the outside is moved to a node inside it and cut where it
    ///     leaves the subgraph's rectangle.
    /// </summary>
    [Fact]
    public void ClusterLayout_Render_LinkFromSubgraphWithOutsideLinks_StartsOutsideIt()
    {
        // Act
        var graph = RenderingTestContext.Render("flowchart TB\n  subgraph A\n    a1 --> a2\n  end\n  subgraph B\n    b1\n  end\n  A -- to B --> B\n  c --> a1\n");

        // Assert
        var scene = RenderingTestContext.SceneOf(graph);
        var start = SceneQuery.Edges(scene)["L_A_B_0"].Segments[0];
        var cluster = SceneQuery.Clusters(scene)["A"];
        Assert.Equal(cluster.X, start.X, 0.002);
        Assert.InRange(start.Y, cluster.Y, cluster.Bottom);
        Assert.Contains("L_A_B_0", SceneQuery.EdgeLabels(scene).Keys);
    }
    /// <summary>
    ///     Checks that a self-loop is laid out through two hidden nodes and drawn as one compact loop outside its node.
    /// </summary>
    [Fact]
    public void ClusterLayout_Render_SelfLoop_IsOneLoopBesideItsNode()
    {
        // Act
        var graph = RenderingTestContext.Render("flowchart TB\n  a --> a\n");

        // Assert
        var scene = RenderingTestContext.SceneOf(graph);
        var edge = Assert.Single(SceneQuery.Edges(scene));
        Assert.Equal("L_a_a_0", edge.Key);
        Assert.Equal(2, SceneQuery.OfRole(scene, "dummy-group").Count);
        var node = SceneQuery.Nodes(scene)["flowchart-a-0"];
        Assert.All(edge.Value.Segments, s => Assert.False(s.X > node.Box.X + 0.001 && s.X < node.Box.Right - 0.001 && s.Y > node.Box.Y + 0.001 && s.Y < node.Box.Bottom - 0.001));
    }

    /// <summary>
    ///     Checks that a subgraph title wider than its contents widens the rectangle around the same center.
    /// </summary>
    [Fact]
    public void ClusterLayout_Render_WideTitle_WidensTheRectangle()
    {
        // Act
        var graph = RenderingTestContext.Render("flowchart TB\n  subgraph S [A rather long title for one small node]\n    a\n  end\n  x --> a\n");

        // Assert
        var scene = RenderingTestContext.SceneOf(graph);
        var cluster = SceneQuery.Clusters(scene)["S"];
        var node = SceneQuery.Nodes(scene)["flowchart-a-0"];
        var title = Assert.Single(SceneQuery.OfRole(scene, "label"), item => item.Id == "S");
        Assert.True(cluster.Width > node.Box.Width + 100);
        Assert.Equal(cluster.X + (cluster.Width / 2), node.X, 6);
        Assert.True(cluster.Width >= Assert.IsType<SceneText>(title).Box.Width + 8);
    }

    /// <summary>
    ///     Checks that the subgraph title margins make the rectangle taller and move its nodes down.
    /// </summary>
    [Fact]
    public void ClusterLayout_Render_SubgraphTitleMargins_MoveNodesDown()
    {
        // Arrange
        const string Body = "flowchart TB\n  subgraph S\n    a\n  end\n  x --> a\n";
        const string Margins = "---\nconfig:\n  flowchart:\n    subGraphTitleMargin:\n      top: 10\n      bottom: 6\n---\n";

        // Act
        var plain = RenderingTestContext.SceneOf(RenderingTestContext.Render(Body));
        var margined = RenderingTestContext.SceneOf(RenderingTestContext.Render(Margins + Body));

        // Assert
        Assert.Equal(SceneQuery.Clusters(plain)["S"].Height + 16, SceneQuery.Clusters(margined)["S"].Height, 6);
        Assert.Equal(SceneQuery.Nodes(plain)["flowchart-a-0"].Y + 8, SceneQuery.Nodes(margined)["flowchart-a-0"].Y, 6);
    }

    /// <summary>
    ///     Checks that nested isolated subgraphs are laid out recursively, each as its own group.
    /// </summary>
    [Fact]
    public void ClusterLayout_Render_NestedIsolatedSubgraph_IsANestedGroup()
    {
        // Act
        var graph = RenderingTestContext.Render("flowchart LR\n  subgraph O[Outer]\n    o1\n    subgraph I[Inner]\n      i1 --> i2\n    end\n  end\n  start --> o1\n");

        // Assert
        var scene = RenderingTestContext.SceneOf(graph);
        Assert.Equal("I", Assert.Single(SceneQuery.OfRole(scene, "subgraph")).Id);
        Assert.Equal(["I", "O"], SceneQuery.Clusters(scene).Keys.Order(StringComparer.Ordinal));
        Assert.True(SceneQuery.Clusters(scene)["O"].Encloses(SceneQuery.Clusters(scene)["I"]));
    }

    /// <summary>
    ///     Checks the drawing order of <c>sortNodesByHierarchy</c>: the top level first, then each subtree.
    /// </summary>
    [Fact]
    public void ClusterLayout_SortNodesByHierarchy_TopLevelFirst()
    {
        // Arrange
        var graph = new Graph<DagreGraph, ClusterLayoutNode, ClusterLayoutEdge>(multigraph: true, compound: true);
        graph.SetNode("C");
        graph.SetNode("A");
        graph.SetParent("A", "C");
        graph.SetNode("D");
        graph.SetParent("D", "A");
        graph.SetNode("B");

        // Act
        var order = ClusterLayout.SortNodesByHierarchy(graph);

        // Assert
        Assert.Equal(["C", "B", "A", "D"], order);
    }

    /// <summary>
    ///     Checks that an empty diagram draws nothing and has no bounds.
    /// </summary>
    [Fact]
    public void ClusterLayout_Render_EmptyDiagram_DrawsNothing()
    {
        // Act
        var graph = RenderingTestContext.Render("flowchart TD\n");

        // Assert
        Assert.Null(graph.Bounds);
        Assert.Equal(["clusters", "edgePaths", "edgeLabels", "nodes"], graph.Items.Select(item => item.Role));
        Assert.All(graph.Items, item => Assert.Empty(Assert.IsType<SceneGroup>(item).Children));
    }

    /// <summary>
    ///     Checks that the cluster layout charges the work budget and stops with a located limit failure.
    /// </summary>
    [Fact]
    public void ClusterLayout_Render_TinyWorkBudget_ThrowsLimitExceeded()
    {
        // Act
        var exception = Assert.Throws<MermaidRenderException>(() =>
            RenderingTestContext.Render("flowchart TB\n  subgraph S\n    a --> b\n  end\n  c --> S\n", maxWork: 10));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, exception.Code);
        Assert.Equal(1, exception.Location.Line);
    }
}
