using DemaConsulting.CanvasNet.Mermaid.Graphlib;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Graphlib;

/// <summary>
///     Subsystem tests of the Graphlib subsystem: the graph and its traversals together keep JavaScript's key order.
/// </summary>
public class GraphlibTests
{
    /// <summary>
    ///     Validates that a traversal of a graph with numeric and other node names visits the numeric names first, as
    ///     graphlib does in JavaScript.
    /// </summary>
    [Fact]
    public void Graphlib_Traversal_NumericNames_FollowJavaScriptKeyOrder()
    {
        // Arrange
        var g = new Graph<object, string, string>(multigraph: true, compound: true);
        g.SetEdge("root", "z", null, "e1").SetEdge("root", "12", null, "e2").SetEdge("root", "3", null, "e3");
        g.SetEdge("3", "y").SetParent("y", "cluster");

        // Act
        var pre = GraphAlgorithms.Preorder(g, g.Nodes());

        // Assert
        Assert.Equal(["3", "12", "root", "z", "y", "cluster"], g.Nodes());
        Assert.Equal(["3", "y", "12", "root", "z", "cluster"], pre);
        Assert.Equal(["3", "12", "root", "z", "cluster"], g.Children()!);
        Assert.Equal(["y"], g.Children("cluster")!);
    }

    /// <summary>
    ///     Validates that removing and adding nodes and edges keeps the graph consistent for the traversals.
    /// </summary>
    [Fact]
    public void Graphlib_Mutation_RemoveAndAdd_TraversalsSeeCurrentGraph()
    {
        // Arrange
        var g = new Graph<object, string, string>();
        g.SetPath(["a", "b", "c", "a"]);

        // Act
        g.RemoveEdge("c", "a");
        var acyclic = GraphAlgorithms.FindCycles(g);
        g.SetEdge("c", "a");
        var cyclic = GraphAlgorithms.FindCycles(g);

        // Assert
        Assert.Empty(acyclic);
        Assert.Single(cyclic);
        Assert.Equal(3, cyclic[0].Count);
    }
}
