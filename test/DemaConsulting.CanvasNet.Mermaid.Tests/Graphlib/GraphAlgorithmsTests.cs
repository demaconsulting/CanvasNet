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
}
