using DemaConsulting.CanvasNet.Mermaid.Graphlib;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Graphlib;

/// <summary>
///     Unit tests of <see cref="Graph{TGraph,TNode,TEdge}"/>: the graphlib port and its JavaScript key order.
/// </summary>
public class GraphTests
{
    /// <summary>
    ///     Validates the defaults of a new graph.
    /// </summary>
    [Fact]
    public void Graph_Constructor_Defaults_DirectedSimpleFlat()
    {
        // Act
        var g = new Graph<string, string, string>();

        // Assert
        Assert.True(g.IsDirected);
        Assert.False(g.IsMultigraph);
        Assert.False(g.IsCompound);
        Assert.Equal(0, g.NodeCount);
        Assert.Equal(0, g.EdgeCount);
        Assert.Null(g.GraphLabel);
        Assert.Equal("label", g.SetGraph("label").GraphLabel);
    }

    /// <summary>
    ///     Validates that nodes are listed with numeric names first in numeric order, then the others in insertion
    ///     order, as JavaScript lists the keys of an object.
    /// </summary>
    [Fact]
    public void Graph_Nodes_NumericNames_ComeFirstInNumericOrder()
    {
        // Arrange
        var g = new Graph<object, string, string>();

        // Act
        g.SetNode("b").SetNode("10").SetNode("a").SetNode("2").SetNode("1");

        // Assert
        Assert.Equal(["1", "2", "10", "b", "a"], g.Nodes());
        Assert.Equal(5, g.NodeCount);
    }

    /// <summary>
    ///     Validates that successors and predecessors follow the same key order.
    /// </summary>
    [Fact]
    public void Graph_Successors_NumericNames_ComeFirst()
    {
        // Arrange
        var g = new Graph<object, string, string>();
        g.SetEdge("x", "z").SetEdge("x", "5").SetEdge("x", "y").SetEdge("x", "3");
        g.SetEdge("q", "x").SetEdge("9", "x");

        // Act
        var successors = g.Successors("x")!;
        var predecessors = g.Predecessors("x")!;

        // Assert
        Assert.Equal(["3", "5", "z", "y"], successors);
        Assert.Equal(["9", "q"], predecessors);
        Assert.Equal(["9", "q", "3", "5", "z", "y"], g.Neighbors("x")!);
        Assert.Null(g.Successors("missing"));
    }

    /// <summary>
    ///     Validates that a node label given once is kept by a later call without a label and replaced by one with a
    ///     label, and that the default label applies to new nodes only.
    /// </summary>
    [Fact]
    public void Graph_SetNode_ExistingNode_KeepsOrReplacesLabel()
    {
        // Arrange
        var g = new Graph<object, string, string>().SetDefaultNodeLabel(v => "default-" + v);

        // Act
        g.SetNode("a", "first");
        g.SetNode("a");
        g.SetNode("b");
        g.SetNode("c", "third").SetNode("c", "replaced");

        // Assert
        Assert.Equal("first", g.Node("a"));
        Assert.Equal("default-b", g.Node("b"));
        Assert.Equal("replaced", g.Node("c"));
        Assert.Null(g.Node("missing"));
    }

    /// <summary>
    ///     Validates that removing a node removes its edges, and that a node added again goes to the end.
    /// </summary>
    [Fact]
    public void Graph_RemoveNode_WithEdges_RemovesEdgesAndReordersOnReAdd()
    {
        // Arrange
        var g = new Graph<object, string, string>(multigraph: true);
        g.SetEdge("a", "b").SetEdge("b", "c").SetEdge("b", "b", "loop", "self").SetNode("d");

        // Act
        g.RemoveNode("b").RemoveNode("missing");
        g.SetNode("b");

        // Assert
        Assert.Equal(["a", "c", "d", "b"], g.Nodes());
        Assert.Equal(0, g.EdgeCount);
        Assert.Empty(g.Successors("a")!);
        Assert.Empty(g.Predecessors("c")!);
    }

    /// <summary>
    ///     Validates the parents and children of a compound graph, and that removing a parent moves its children to the
    ///     top level.
    /// </summary>
    [Fact]
    public void Graph_SetParent_Compound_TracksChildren()
    {
        // Arrange
        var g = new Graph<object, string, string>(compound: true);

        // Act
        g.SetParent("a", "parent").SetParent("b", "parent").SetParent("parent", "outer").SetNode("c");

        // Assert
        Assert.Equal("parent", g.Parent("a"));
        Assert.Equal(["a", "b"], g.Children("parent")!);
        Assert.Equal(["outer", "c"], g.Children()!);
        Assert.Equal(["parent"], g.Children("outer")!);
        Assert.Null(g.Parent("outer"));
        Assert.Null(g.Children("missing"));

        g.RemoveNode("parent");
        Assert.Equal(["outer", "c", "a", "b"], g.Children()!);
        Assert.Null(g.Parent("a"));
    }

    /// <summary>
    ///     Validates that a parent cycle and a parent in a flat graph are refused.
    /// </summary>
    [Fact]
    public void Graph_SetParent_CycleOrFlatGraph_Throws()
    {
        // Arrange
        var compound = new Graph<object, string, string>(compound: true);
        compound.SetParent("a", "b");
        var flat = new Graph<object, string, string>();

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => compound.SetParent("b", "a"));
        Assert.Throws<InvalidOperationException>(() => flat.SetParent("a", "b"));
        Assert.Equal(["a", "b"], flat.SetNode("a").SetNode("b").Children()!);
        Assert.Empty(flat.Children("a")!);
        Assert.Null(flat.Children("missing"));
    }

    /// <summary>
    ///     Validates named edges of a multigraph, edge labels and the default edge label.
    /// </summary>
    [Fact]
    public void Graph_SetEdge_Multigraph_KeepsParallelEdgesApart()
    {
        // Arrange
        var g = new Graph<object, string, string>(multigraph: true).SetDefaultEdgeLabel((v, w, name) => $"{v}{w}{name}");

        // Act
        g.SetEdge("a", "b").SetEdge("a", "b", "named", "n1").SetEdge("a", "b", "other", "n2").SetEdge("a", "b");

        // Assert
        Assert.Equal(3, g.EdgeCount);
        Assert.Equal("ab", g.Edge("a", "b"));
        Assert.Equal("named", g.Edge(new GraphEdge("a", "b", "n1")));
        Assert.Equal([new GraphEdge("a", "b"), new GraphEdge("a", "b", "n1"), new GraphEdge("a", "b", "n2")], g.Edges());
        Assert.Single(g.OutEdges("a", "b")!, e => e.Name == "n2");
        Assert.True(g.HasEdge("a", "b", "n1"));
        Assert.False(g.HasEdge("b", "a"));
        g.RemoveEdge("a", "b", "n1");
        Assert.Equal(2, g.EdgeCount);
        Assert.Equal(["b"], g.Successors("a")!);
    }

    /// <summary>
    ///     Validates that a simple graph refuses a named edge.
    /// </summary>
    [Fact]
    public void Graph_SetEdge_NamedEdgeInSimpleGraph_Throws()
    {
        // Arrange
        var g = new Graph<object, string, string>();

        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => g.SetEdge("a", "b", "label", "name"));
    }

    /// <summary>
    ///     Validates that an undirected graph stores an edge with its ends in ordinal order and finds it either way.
    /// </summary>
    [Fact]
    public void Graph_SetEdge_Undirected_FindsEdgeEitherWay()
    {
        // Arrange
        var g = new Graph<object, string, string>(directed: false);

        // Act
        g.SetEdge("b", "a", "label");

        // Assert
        Assert.Equal([new GraphEdge("a", "b")], g.Edges());
        Assert.Equal("label", g.Edge("a", "b"));
        Assert.Equal("label", g.Edge("b", "a"));
        Assert.Equal(["a"], g.Neighbors("b")!);
        Assert.False(g.IsLeaf("a"));
    }

    /// <summary>
    ///     Validates in-edges, out-edges and node edges, with and without the other end.
    /// </summary>
    [Fact]
    public void Graph_NodeEdges_FilteredByOtherEnd_ListsMatchingEdges()
    {
        // Arrange
        var g = new Graph<object, string, string>();
        g.SetEdge("a", "b").SetEdge("b", "c").SetEdge("c", "b");

        // Act
        var all = g.NodeEdges("b")!;
        var withC = g.NodeEdges("b", "c")!;

        // Assert
        Assert.Equal([new GraphEdge("a", "b"), new GraphEdge("c", "b"), new GraphEdge("b", "c")], all);
        Assert.Equal([new GraphEdge("c", "b"), new GraphEdge("b", "c")], withC);
        Assert.Equal([new GraphEdge("a", "b")], g.InEdges("b", "a")!);
        Assert.Null(g.InEdges("missing"));
        Assert.Equal(["a"], g.Sources());
        Assert.Empty(g.Sinks());
    }

    /// <summary>
    ///     Validates that a filtered copy keeps the passing nodes, the edges between them, and each node under its
    ///     nearest passing ancestor.
    /// </summary>
    [Fact]
    public void Graph_FilterNodes_Compound_ReparentsToNearestKeptAncestor()
    {
        // Arrange
        var g = new Graph<string, string, string>(multigraph: true, compound: true).SetGraph("label");
        g.SetParent("leaf", "middle").SetParent("middle", "top");
        g.SetEdge("leaf", "other", "e1", "n").SetEdge("middle", "leaf", "e2");

        // Act
        var copy = g.FilterNodes(v => v != "middle");

        // Assert
        Assert.Equal("label", copy.GraphLabel);
        Assert.Equal(["leaf", "top", "other"], copy.Nodes());
        Assert.Equal("top", copy.Parent("leaf"));
        Assert.Equal([new GraphEdge("leaf", "other", "n")], copy.Edges());
        Assert.True(copy.IsMultigraph && copy.IsCompound);
    }

    /// <summary>
    ///     Validates that a path adds an edge between each pair of consecutive nodes.
    /// </summary>
    [Fact]
    public void Graph_SetPath_Nodes_AddsConsecutiveEdges()
    {
        // Arrange
        var g = new Graph<object, string, string>();

        // Act
        g.SetPath(["a", "b", "c"], "label").SetPath(["c", "d"]).SetNodes(["e"]);

        // Assert
        Assert.Equal(3, g.EdgeCount);
        Assert.Equal("label", g.Edge("b", "c"));
        Assert.Null(g.Edge("c", "d"));
        Assert.True(g.HasNode("e"));
        Assert.True(g.IsLeaf("d"));
    }
}
