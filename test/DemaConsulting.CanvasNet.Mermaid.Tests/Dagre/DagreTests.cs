using System.Globalization;
using DemaConsulting.CanvasNet.Mermaid.Dagre;
using DemaConsulting.CanvasNet.Mermaid.Graphlib;
using static DemaConsulting.CanvasNet.Mermaid.Tests.Dagre.DagreTestGraphs;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Dagre;

/// <summary>
///     Subsystem tests of the Dagre subsystem: whole layouts compared with the stage goldens recorded from dagre-d3-es,
///     and the work budget, cancellation and determinism of the layout.
/// </summary>
public class DagreTests
{
    /// <summary>The work the complete DAG on 30 nodes takes (pinned).</summary>
    private const long K30Work = 2_955_593;

    /// <summary>The work the small graph takes (pinned).</summary>
    private const long SmallGraphWork = 1_369;

    /// <summary>
    ///     Gets the golden case names.
    /// </summary>
    public static TheoryData<string> GoldenNames => DagreGoldens.Names();

    /// <summary>
    ///     Validates that the port lays out every golden graph exactly as dagre-d3-es does: ranks and orders equal,
    ///     coordinates, sizes, routes and label centers within 1e-9.
    /// </summary>
    /// <param name="name">The golden case name.</param>
    [Theory]
    [MemberData(nameof(GoldenNames))]
    public void Dagre_Layout_UpstreamGolden_MatchesDagre(string name)
    {
        // Arrange
        var layout = DagreGoldens.Case(name);

        // Act
        var difference = DagreGoldens.Difference(layout);

        // Assert
        Assert.Null(difference);
    }

    /// <summary>
    ///     Validates that the complete DAG on 30 nodes (435 edges, thousands of dummy nodes) stops with a located
    ///     <c>mermaid-limit-exceeded</c> under a small work budget.
    /// </summary>
    [Fact]
    public void Dagre_Layout_CompleteGraph30_SmallBudget_LimitExceeded()
    {
        // Arrange
        var g = CompleteDag(30);
        var location = new MermaidLocation("k30.mmd", 1, 1);
        var meter = new LayoutWorkMeter(1_000_000, location, CancellationToken.None);

        // Act
        var ex = Assert.Throws<MermaidRenderException>(() => DagreLayout.Run(g, meter));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(location, ex.Location);
        Assert.Equal(1_000_000, meter.Used);
    }

    /// <summary>
    ///     Validates that the default budget lays the complete DAG on 30 nodes out, and pins the work it takes.
    /// </summary>
    [Fact]
    public void Dagre_Layout_CompleteGraph30_DefaultBudget_CompletesWithPinnedWork()
    {
        // Arrange
        var g = CompleteDag(30);
        var meter = new LayoutWorkMeter(MermaidLimits.Default.MaxLayoutWork, default, CancellationToken.None);

        // Act
        DagreLayout.Run(g, meter);

        // Assert
        Assert.Equal(K30Work, meter.Used);
        Assert.All(g.Nodes(), v => Assert.True(double.IsFinite(g.Node(v)!.X)));
    }

    /// <summary>
    ///     Validates that the work charged for a small graph is pinned, so that a change to the metering is noticed.
    /// </summary>
    [Fact]
    public void Dagre_Layout_SmallGraph_PinnedWork()
    {
        // Arrange
        var g = Input(new DagreGraph { RankDir = "TB" });
        foreach (var v in new[] { "a", "b", "c", "d" })
        {
            g.SetNode(v, Node(40, 20));
        }

        g.SetPath(["a", "b", "c", "a"]).SetEdge("a", "d", new DagreEdge { Width = 30, Height = 12 });
        g.SetParent("c", "cluster");
        var meter = Meter();

        // Act
        DagreLayout.Run(g, meter);

        // Assert
        Assert.Equal(SmallGraphWork, meter.Used);
    }

    /// <summary>
    ///     Validates that a canceled layout stops with <see cref="OperationCanceledException"/>.
    /// </summary>
    [Fact]
    public void Dagre_Layout_Canceled_ThrowsOperationCanceled()
    {
        // Arrange
        using var source = new CancellationTokenSource();
        source.Cancel();
        var meter = new LayoutWorkMeter(long.MaxValue, default, source.Token);

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => DagreLayout.Run(CompleteDag(5), meter));
    }

    /// <summary>
    ///     Validates that layouts of the same graph give identical results, sequentially and in parallel.
    /// </summary>
    [Fact]
    public void Dagre_Layout_RepeatedAndParallel_Identical()
    {
        // Arrange
        var layout = DagreGoldens.Case("nested-001");

        // Act
        var fingerprints = Enumerable.Range(0, 8).AsParallel().Select(_ =>
        {
            var g = DagreGoldens.Build(layout);
            DagreLayout.Run(g, Meter());
            return Fingerprint(g);
        }).ToList();

        // Assert
        Assert.Single(fingerprints.Distinct(StringComparer.Ordinal));
    }

    /// <summary>
    ///     Gets a text with every node center and route point of a laid-out graph.
    /// </summary>
    /// <param name="g">The graph.</param>
    /// <returns>The text.</returns>
    private static string Fingerprint(Graph<DagreGraph, DagreNode, DagreEdge> g) =>
        string.Join('|', g.Nodes().Select(v => string.Create(CultureInfo.InvariantCulture, $"{v}:{g.Node(v)!.X:R},{g.Node(v)!.Y:R}"))) + "#" +
        string.Join('|', g.Edges().Select(e => string.Join(';', g.Edge(e)!.Points!.Select(p => string.Create(CultureInfo.InvariantCulture, $"{p.X:R},{p.Y:R}")))));

    /// <summary>
    ///     Builds the complete DAG on a number of nodes (an edge from each node to every later node).
    /// </summary>
    /// <param name="n">The number of nodes.</param>
    /// <returns>The graph.</returns>
    private static Graph<DagreGraph, DagreNode, DagreEdge> CompleteDag(int n)
    {
        var g = Input(new DagreGraph { RankDir = "TB", NodeSep = 50, RankSep = 50, MarginX = 8, MarginY = 8 });
        for (var i = 0; i < n; i++)
        {
            g.SetNode("n" + i, Node(60, 30));
        }

        for (var i = 0; i < n; i++)
        {
            for (var j = i + 1; j < n; j++)
            {
                g.SetEdge("n" + i, "n" + j, new DagreEdge(), string.Create(CultureInfo.InvariantCulture, $"e{i}-{j}"));
            }
        }

        return g;
    }
}
