using DemaConsulting.CanvasNet.Mermaid.Dagre;
using DemaConsulting.CanvasNet.Mermaid.Graphlib;
using static DemaConsulting.CanvasNet.Mermaid.Tests.Dagre.DagreTestGraphs;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Dagre;

/// <summary>
///     Unit tests of <see cref="DagreOrdering"/>, with the cases of dagre's <c>cross-count.test.js</c>,
///     <c>sort.test.js</c>, <c>resolve-conflicts.test.js</c>, <c>barycenter.test.js</c>, <c>init-order.test.js</c> and
///     <c>add-subgraph-constraints.test.js</c>.
/// </summary>
public class DagreOrderingTests
{
    /// <summary>
    ///     Validates the weighted crossing counts of small layerings.
    /// </summary>
    /// <param name="edges">The edges as <c>v-w</c> or <c>v-w:weight</c>.</param>
    /// <param name="layering">The ranks, each a space-separated list.</param>
    /// <param name="expected">The expected count.</param>
    [Theory]
    [InlineData(new string[0], new string[0], 0)]
    [InlineData(new[] { "a1-b1", "a2-b2" }, new[] { "a1 a2", "b1 b2" }, 0)]
    [InlineData(new[] { "a1-b1", "a2-b2" }, new[] { "a1 a2", "b2 b1" }, 1)]
    [InlineData(new[] { "a1-b1:2", "a2-b2:3" }, new[] { "a1 a2", "b2 b1" }, 6)]
    [InlineData(new[] { "a1-b1", "b1-c1", "a2-b2", "b2-c2" }, new[] { "a1 a2", "b2 b1", "c1 c2" }, 2)]
    [InlineData(new[] { "a-b", "b-c", "d-e", "e-c", "a-f", "f-i", "a-e" }, new[] { "a d", "b e f", "c i" }, 1)]
    [InlineData(new[] { "a-b", "b-c", "d-e", "e-c", "a-f", "f-i", "a-e" }, new[] { "d a", "e b f", "c i" }, 0)]
    public void DagreOrdering_CrossCount_Layering_CountsWeightedCrossings(string[] edges, string[] layering, double expected)
    {
        // Arrange
        var g = Layout(multigraph: false);
        foreach (var edge in edges)
        {
            var parts = edge.Split(':');
            var ends = parts[0].Split('-');
            g.SetEdge(ends[0], ends[1], new LayoutEdge { Weight = parts.Length > 1 ? double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) : 1 });
        }

        var layers = layering.Select(layer => layer.Split(' ').ToList()).ToList();

        // Act
        var count = DagreOrdering.CrossCount(g, layers, Meter());

        // Assert
        Assert.Equal(expected, count);
    }

    /// <summary>
    ///     Validates the barycenter sort: by barycenter, ties by index (biased left or right), entries without a
    ///     barycenter kept at their index.
    /// </summary>
    /// <param name="entries">The entries as <c>vs/i/barycenter/weight</c>, with comma-separated nodes and empty parts for none.</param>
    /// <param name="biasRight">Whether ties go right.</param>
    /// <param name="expectedVs">The expected nodes.</param>
    /// <param name="expectedBarycenter">The expected barycenter, or NaN for none.</param>
    /// <param name="expectedWeight">The expected weight, or NaN for none.</param>
    [Theory]
    [InlineData(new[] { "a/0/2/3", "b/1/1/2" }, false, "b a", 8.0 / 5, 5)]
    [InlineData(new[] { "a,c,d/0/2/3", "b/1/1/2" }, false, "b a c d", 8.0 / 5, 5)]
    [InlineData(new[] { "a/0/1/1", "b/1/1/1" }, false, "a b", 1, 2)]
    [InlineData(new[] { "a/0/1/1", "b/1/1/1" }, true, "b a", 1, 2)]
    [InlineData(new[] { "a/0/2/1", "b/1/6/1", "c/2//", "d/3/3/1" }, false, "a d c b", 11.0 / 3, 3)]
    [InlineData(new[] { "a/0//", "b/3//", "c/2//", "d/1//" }, false, "a d c b", double.NaN, double.NaN)]
    [InlineData(new[] { "a/0/0/1", "b/3//", "c/2//", "d/1//" }, false, "a d c b", 0, 1)]
    public void DagreOrdering_Sort_Entries_SortedByBarycenter(string[] entries, bool biasRight, string expectedVs, double expectedBarycenter, double expectedWeight)
    {
        // Arrange
        var input = entries.Select(text =>
        {
            var parts = text.Split('/');
            var entry = new DagreOrdering.ConflictEntry([.. parts[0].Split(',')], int.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture));
            if (parts[2].Length > 0)
            {
                entry.Barycenter = double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);
                entry.Weight = double.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture);
            }

            return entry;
        }).ToList();

        // Act
        var result = DagreOrdering.Sort(input, biasRight);

        // Assert
        Assert.Equal(expectedVs.Split(' '), result.Vs);
        Assert.Equal(double.IsNaN(expectedBarycenter) ? null : expectedBarycenter, result.Barycenter);
        Assert.Equal(double.IsNaN(expectedWeight) ? null : expectedWeight, result.Weight);
    }

    /// <summary>
    ///     Validates that entries without constraints, or whose constraints hold, are returned unchanged.
    /// </summary>
    /// <param name="constraint">The constraint edge, or null.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("b-a")]
    [InlineData("c-d")]
    public void DagreOrdering_ResolveConflicts_NoConflict_Unchanged(string? constraint)
    {
        // Arrange
        var cg = new Graph<object, object, object>();
        if (constraint is not null)
        {
            cg.SetEdge(constraint[..1], constraint[2..]);
        }

        var input = new List<DagreOrdering.BarycenterEntry>
        {
            new("a") { Barycenter = 2, Weight = 3 },
            new("b") { Barycenter = 1, Weight = 2 },
        };

        // Act
        var results = DagreOrdering.ResolveConflicts(input, cg).OrderBy(e => e.Vs[0], StringComparer.Ordinal).ToList();

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Equal(("a", 0, 2.0, 3.0), (string.Join(' ', results[0].Vs), results[0].I, results[0].Barycenter, results[0].Weight));
        Assert.Equal(("b", 1, 1.0, 2.0), (string.Join(' ', results[1].Vs), results[1].I, results[1].Barycenter, results[1].Weight));
    }

    /// <summary>
    ///     Validates that entries are merged when a constraint conflicts with their barycenters.
    /// </summary>
    [Fact]
    public void DagreOrdering_ResolveConflicts_Conflicts_Coalesced()
    {
        // Arrange
        var pair = new Graph<object, object, object>().SetEdge("a", "b");
        var chain = new Graph<object, object, object>().SetPath(["a", "b", "c", "d"]);
        var weighted = new List<DagreOrdering.BarycenterEntry> { new("a") { Barycenter = 2, Weight = 3 }, new("b") { Barycenter = 1, Weight = 2 } };
        var four = new[] { "a", "b", "c", "d" }.Select((v, i) => new DagreOrdering.BarycenterEntry(v) { Barycenter = 4 - i, Weight = 1 }).ToList();

        // Act
        var merged = DagreOrdering.ResolveConflicts(weighted, pair);
        var mergedChain = DagreOrdering.ResolveConflicts(four, chain);

        // Assert
        Assert.Equal(("a b", 0, 8.0 / 5, 5.0), (string.Join(' ', merged.Single().Vs), merged[0].I, merged[0].Barycenter, merged[0].Weight));
        Assert.Equal(("a b c d", 0, 2.5, 4.0), (string.Join(' ', mergedChain.Single().Vs), mergedChain[0].I, mergedChain[0].Barycenter, mergedChain[0].Weight));
    }

    /// <summary>
    ///     Validates that an entry without a barycenter is kept alone without a constraint and merged with one.
    /// </summary>
    [Fact]
    public void DagreOrdering_ResolveConflicts_NoBarycenter_MergedOnlyWithConstraint()
    {
        // Arrange
        List<DagreOrdering.BarycenterEntry> Input() => [new("a"), new("b") { Barycenter = 1, Weight = 2 }];

        // Act
        var free = DagreOrdering.ResolveConflicts(Input(), new Graph<object, object, object>()).OrderBy(e => e.Vs[0], StringComparer.Ordinal).ToList();
        var before = DagreOrdering.ResolveConflicts(Input(), new Graph<object, object, object>().SetEdge("a", "b"));
        var after = DagreOrdering.ResolveConflicts(Input(), new Graph<object, object, object>().SetEdge("b", "a"));

        // Assert
        Assert.Equal(("a", 0, (double?)null), (string.Join(' ', free[0].Vs), free[0].I, free[0].Barycenter));
        Assert.Equal(("a b", 0, 1.0, 2.0), (string.Join(' ', before.Single().Vs), before[0].I, before[0].Barycenter, before[0].Weight));
        Assert.Equal(("b a", 0, 1.0, 2.0), (string.Join(' ', after.Single().Vs), after[0].I, after[0].Barycenter, after[0].Weight));
    }

    /// <summary>
    ///     Validates the barycenters of a movable layer: none without predecessors, else the weighted average order.
    /// </summary>
    [Fact]
    public void DagreOrdering_Barycenters_MovableLayer_WeightedAverages()
    {
        // Arrange
        var g = new Graph<DagreOrdering.LayerGraphLabel, DagreOrdering.LayerNode, DagreOrdering.LayerEdge>()
            .SetDefaultNodeLabel(_ => new DagreOrdering.LayerNode(new LayoutNode()))
            .SetDefaultEdgeLabel((_, _, _) => new DagreOrdering.LayerEdge(1));
        g.SetNode("a", new DagreOrdering.LayerNode(new LayoutNode { Order = 1 }));
        g.SetNode("b", new DagreOrdering.LayerNode(new LayoutNode { Order = 2 }));
        g.SetNode("c", new DagreOrdering.LayerNode(new LayoutNode { Order = 4 }));
        g.SetEdge("a", "x").SetEdge("b", "x").SetNode("y").SetEdge("a", "z", new DagreOrdering.LayerEdge(2)).SetEdge("c", "z");

        // Act
        var results = DagreOrdering.Barycenters(g, ["x", "y", "z"]);

        // Assert
        Assert.Equal(("x", 1.5, 2.0), (results[0].V, results[0].Barycenter, results[0].Weight));
        Assert.Equal(("y", (double?)null, (double?)null), (results[1].V, results[1].Barycenter, results[1].Weight));
        Assert.Equal(("z", 2.0, 3.0), (results[2].V, results[2].Barycenter, results[2].Weight));
    }

    /// <summary>
    ///     Validates that the initial order places each node by a depth-first search from the nodes in rank order.
    /// </summary>
    [Fact]
    public void DagreOrdering_InitOrder_TwoChains_OrderedBySearch()
    {
        // Arrange
        var g = Layout(compound: true);
        g.SetNode("a", Ranked(0)).SetNode("b", Ranked(1)).SetNode("c", Ranked(2)).SetNode("d", Ranked(3)).SetNode("e", Ranked(2));
        g.SetPath(["a", "b", "c"]).SetPath(["b", "d"]).SetPath(["a", "e"]);

        // Act
        var layers = DagreOrdering.InitOrder(g, Meter());

        // Assert
        Assert.Equal(["a"], layers[0]);
        Assert.Equal(["b"], layers[1]);
        Assert.Equal(["c", "e"], layers[2]);
        Assert.Equal(["d"], layers[3]);
    }

    /// <summary>
    ///     Validates that sibling clusters met in order get a constraint, and that a cluster seen again does not.
    /// </summary>
    [Fact]
    public void DagreOrdering_AddSubgraphConstraints_SiblingClusters_Constrained()
    {
        // Arrange
        var g = new Graph<DagreOrdering.LayerGraphLabel, DagreOrdering.LayerNode, DagreOrdering.LayerEdge>(compound: true);
        g.SetParent("a", "sg1").SetParent("b", "sg1").SetParent("c", "sg2").SetParent("sg1", "root").SetParent("sg2", "root");
        var cg = new Graph<object, object, object>();

        // Act
        DagreOrdering.AddSubgraphConstraints(g, cg, ["a", "b", "c"]);

        // Assert
        Assert.Equal([new GraphEdge("sg1", "sg2")], cg.Edges());
    }

    /// <summary>
    ///     Validates the whole ordering on a graph with a crossing that a sweep removes.
    /// </summary>
    [Fact]
    public void DagreOrdering_Order_CrossingGraph_RemovesCrossing()
    {
        // Arrange
        var g = Layout(compound: true);
        g.SetNode("a", Ranked(0)).SetNode("b", Ranked(0)).SetNode("c", Ranked(1)).SetNode("d", Ranked(1));
        g.SetEdge("a", "d").SetEdge("b", "c").SetEdge("a", "c");

        // Act
        DagreOrdering.Order(g, Meter());
        var layers = DagreLayout.BuildLayerMatrix(g);

        // Assert
        Assert.Equal(0.0, DagreOrdering.CrossCount(g, layers, Meter()));
    }
}
