using System.Globalization;
using DemaConsulting.CanvasNet.Layout.Dagre;
using DemaConsulting.CanvasNet.Layout.Graphlib;
using DemaConsulting.CanvasNet.Layout.Tests.Dagre;

namespace DemaConsulting.CanvasNet.Layout.Tests;

/// <summary>
///     System-level integration tests for the <c>DemaConsulting.CanvasNet.Layout</c> package, exercising the interface
///     its consumers use (an input <see cref="Graph{TGraph,TNode,TEdge}"/> of <see cref="DagreGraph"/>,
///     <see cref="DagreNode"/> and <see cref="DagreEdge"/> labels, <see cref="DagreLayout.Run{TNode,TEdge}"/> and a
///     consumer's <see cref="LayoutWorkMeter"/>) end to end: one test method per top-level system requirement declared in
///     <c>docs/reqstream/canvas-net-layout.yaml</c> and its platform requirements.
/// </summary>
public class LayoutSystemIntegrationTests
{
    /// <summary>
    ///     Validates that a compound, cyclic graph with an edge label is laid out in every rank direction: every node gets
    ///     a finite center, the cluster encloses its members, every edge gets a finite route and the label a finite center,
    ///     the drawing has a size, and the ranks run in the requested direction.
    /// </summary>
    [Fact]
    public void CanvasNetLayout_LayeredLayout_CompoundCyclicLabeledGraph_LaidOutInEveryRankDirection()
    {
        foreach (var rankDir in new[] { "TB", "BT", "LR", "RL" })
        {
            // Arrange
            var g = SampleGraph(rankDir);
            var meter = new TestLayoutWorkMeter(long.MaxValue, CancellationToken.None);

            // Act
            DagreLayout.Run(g, meter);

            // Assert: finite centers and routes, a labeled edge with a finite label center, and a drawing size
            Assert.All(g.Nodes(), v => Assert.True(double.IsFinite(g.Node(v)!.X) && double.IsFinite(g.Node(v)!.Y), v));
            Assert.All(g.Edges(), e =>
            {
                var points = g.Edge(e)!.Points;
                Assert.NotNull(points);
                Assert.True(points.Count >= 2);
                Assert.All(points, p => Assert.True(double.IsFinite(p.X) && double.IsFinite(p.Y)));
            });
            var label = g.Edge("a", "b")!;
            Assert.True(double.IsFinite(label.X!.Value) && double.IsFinite(label.Y!.Value));
            Assert.True(g.GraphLabel!.Width > 0 && g.GraphLabel.Height > 0);

            // Assert: the cluster encloses its members
            var cluster = g.Node("group")!;
            foreach (var member in new[] { "b", "c" })
            {
                var node = g.Node(member)!;
                Assert.True(Math.Abs(node.X - cluster.X) <= cluster.Width!.Value / 2, member);
                Assert.True(Math.Abs(node.Y - cluster.Y) <= cluster.Height!.Value / 2, member);
            }

            // Assert: the first edge of the cycle runs in the rank direction
            var a = g.Node("a")!;
            var b = g.Node("b")!;
            var forward = rankDir switch
            {
                "TB" => b.Y - a.Y,
                "BT" => a.Y - b.Y,
                "LR" => b.X - a.X,
                _ => a.X - b.X,
            };
            Assert.True(forward > 0, rankDir);
        }
    }

    /// <summary>
    ///     Validates that the layout stops with the consumer's limit failure once the work budget the consumer gives is
    ///     used up.
    /// </summary>
    [Fact]
    public void CanvasNetLayout_Limits_SmallBudget_ConsumerLimitFailure()
    {
        // Arrange
        var g = SampleGraph("TB");
        var meter = new TestLayoutWorkMeter(100, CancellationToken.None);

        // Act
        var ex = Assert.Throws<LayoutTestException>(() => DagreLayout.Run(g, meter));

        // Assert
        Assert.True(ex.IsLimit);
        Assert.Equal(100, meter.Used);
    }

    /// <summary>
    ///     Validates that a canceled layout stops with <see cref="OperationCanceledException"/>.
    /// </summary>
    [Fact]
    public void CanvasNetLayout_Cancellation_Canceled_ThrowsOperationCanceled()
    {
        // Arrange
        using var source = new CancellationTokenSource();
        source.Cancel();
        var meter = new TestLayoutWorkMeter(long.MaxValue, source.Token);

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => DagreLayout.Run(SampleGraph("TB"), meter));
    }

    /// <summary>
    ///     Validates that layouts of the same graph give identical results and use the same work, sequentially and in
    ///     parallel.
    /// </summary>
    [Fact]
    public void CanvasNetLayout_Determinism_RepeatedAndParallel_Identical()
    {
        // Act
        var results = Enumerable.Range(0, 8).AsParallel().Select(_ =>
        {
            var g = SampleGraph("LR");
            var meter = new TestLayoutWorkMeter(long.MaxValue, CancellationToken.None);
            DagreLayout.Run(g, meter);
            return Fingerprint(g) + "@" + meter.Used.ToString(CultureInfo.InvariantCulture);
        }).ToList();

        // Assert
        Assert.Single(results.Distinct(StringComparer.Ordinal));
    }

    /// <summary>
    ///     Validates that the license notices of the ported projects (dagre-d3-es with graphlib, and lodash) ship with the
    ///     package together with the provenance record, which states that the project is not affiliated with them.
    /// </summary>
    [Fact]
    public void CanvasNetLayout_ThirdPartyNotices_ShipWithPackage()
    {
        // Arrange
        var folder = Path.Combine(AppContext.BaseDirectory, "Notices");

        // Act
        var dagre = File.ReadAllText(Path.Combine(folder, "LICENSE-dagre.txt"));
        var lodash = File.ReadAllText(Path.Combine(folder, "LICENSE-lodash.txt"));
        var provenance = File.ReadAllText(Path.Combine(folder, "third-party-provenance.txt"));

        // Assert
        Assert.Contains("Copyright (c) 2012-2014 Chris Pettitt", dagre, StringComparison.Ordinal);
        Assert.Contains("dagre-es contributors", dagre, StringComparison.Ordinal);
        Assert.Contains("Copyright OpenJS Foundation and other contributors", lodash, StringComparison.Ordinal);
        Assert.Contains("not affiliated", provenance, StringComparison.Ordinal);
        Assert.Contains("LICENSE-dagre.txt", provenance, StringComparison.Ordinal);
        Assert.Contains("LICENSE-lodash.txt", provenance, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Builds the sample graph: <c>a</c> and <c>d</c> at the top level, <c>b</c> and <c>c</c> in the cluster
    ///     <c>group</c>, the cycle <c>a -> b -> c -> a</c> (whose first edge has a label) and the edge <c>c -> d</c>.
    /// </summary>
    /// <param name="rankDir">The rank direction.</param>
    /// <returns>The input graph, as a consumer builds it.</returns>
    private static Graph<DagreGraph, DagreNode, DagreEdge> SampleGraph(string rankDir)
    {
        var g = new Graph<DagreGraph, DagreNode, DagreEdge>(multigraph: true, compound: true)
            .SetGraph(new DagreGraph { RankDir = rankDir, NodeSep = 50, RankSep = 50, MarginX = 8, MarginY = 8 })
            .SetDefaultEdgeLabel((_, _, _) => new DagreEdge());
        foreach (var v in new[] { "a", "b", "c", "d" })
        {
            g.SetNode(v, new DagreNode { Width = 60, Height = 30 });
        }

        g.SetNode("group", new DagreNode());
        g.SetParent("b", "group");
        g.SetParent("c", "group");
        g.SetEdge("a", "b", new DagreEdge { Width = 30, Height = 12, LabelPos = "c" });
        g.SetEdge("b", "c", new DagreEdge());
        g.SetEdge("c", "a", new DagreEdge());
        g.SetEdge("c", "d", new DagreEdge());
        return g;
    }

    /// <summary>
    ///     Gets a text with every node center and every route point of a laid-out graph.
    /// </summary>
    /// <param name="g">The graph.</param>
    /// <returns>The text.</returns>
    private static string Fingerprint(Graph<DagreGraph, DagreNode, DagreEdge> g) =>
        string.Join('|', g.Nodes().Select(v => string.Create(CultureInfo.InvariantCulture, $"{v}:{g.Node(v)!.X:R},{g.Node(v)!.Y:R}"))) + "#" +
        string.Join('|', g.Edges().Select(e => string.Join(';', g.Edge(e)!.Points!.Select(p => string.Create(CultureInfo.InvariantCulture, $"{p.X:R},{p.Y:R}")))));
}
