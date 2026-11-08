using DemaConsulting.CanvasNet.Mermaid.Graphlib;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Graphlib;

/// <summary>
///     Unit tests of <see cref="GraphAlgorithms"/>, with cases from graphlib's own tests.
/// </summary>
public class GraphAlgorithmsTests
{
    /// <summary>
    ///     Validates preorder and postorder from one root, following successors in key order.
    /// </summary>
    [Fact]
    public void GraphAlgorithms_PreorderPostorder_Tree_VisitsInOrder()
    {
        // Arrange
        var g = new Graph<object, object, object>();
        g.SetPath(["a", "b", "d"]).SetPath(["a", "c"]).SetEdge("b", "e");

        // Act
        var pre = GraphAlgorithms.Preorder(g, ["a"]);
        var post = GraphAlgorithms.Postorder(g, ["a"]);

        // Assert
        Assert.Equal(["a", "b", "d", "e", "c"], pre);
        Assert.Equal(["d", "e", "b", "c", "a"], post);
    }

    /// <summary>
    ///     Validates that an undirected graph is walked through its neighbors and that an unknown root is refused.
    /// </summary>
    [Fact]
    public void GraphAlgorithms_Preorder_UndirectedAndUnknownRoot_FollowsNeighborsOrThrows()
    {
        // Arrange
        var g = new Graph<object, object, object>(directed: false);
        g.SetEdge("b", "a").SetEdge("b", "c");

        // Act
        var pre = GraphAlgorithms.Preorder(g, ["c"]);

        // Assert
        Assert.Equal(["c", "b", "a"], pre);
        Assert.Throws<ArgumentException>(() => GraphAlgorithms.Postorder(g, ["missing"]));
    }

    /// <summary>
    ///     Validates the connected components, ignoring direction.
    /// </summary>
    [Fact]
    public void GraphAlgorithms_Components_TwoParts_ListsBoth()
    {
        // Arrange
        var g = new Graph<object, object, object>();
        g.SetPath(["a", "b", "c"]).SetEdge("e", "d").SetNode("f");

        // Act
        var components = GraphAlgorithms.Components(g);

        // Assert
        Assert.Equal(3, components.Count);
        Assert.Equal(["a", "b", "c"], components[0]);
        Assert.Equal(["e", "d"], components[1]);
        Assert.Equal(["f"], components[2]);
    }

    /// <summary>
    ///     Validates Tarjan's strongly connected components and the cycles found from them.
    /// </summary>
    [Fact]
    public void GraphAlgorithms_TarjanFindCycles_CyclesAndSelfLoop_AreFound()
    {
        // Arrange
        var g = new Graph<object, object, object>();
        g.SetPath(["a", "b", "c", "a"]).SetEdge("c", "d").SetEdge("e", "e").SetNode("f");

        // Act
        var components = GraphAlgorithms.Tarjan(g);
        var cycles = GraphAlgorithms.FindCycles(g);

        // Assert
        Assert.Equal(4, components.Count);
        Assert.Equal(["d"], components[0]);
        Assert.Equal(["c", "b", "a"], components[1]);
        Assert.Equal(2, cycles.Count);
        Assert.Equal(["c", "b", "a"], cycles[0]);
        Assert.Equal(["e"], cycles[1]);
    }

    /// <summary>
    ///     Validates that an acyclic graph has no cycles.
    /// </summary>
    [Fact]
    public void GraphAlgorithms_FindCycles_AcyclicGraph_ReturnsNone()
    {
        // Arrange
        var g = new Graph<object, object, object>();
        g.SetPath(["a", "b", "c"]).SetEdge("a", "c");

        // Act
        var cycles = GraphAlgorithms.FindCycles(g);

        // Assert
        Assert.Empty(cycles);
    }
}
