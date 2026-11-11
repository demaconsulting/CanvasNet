using DemaConsulting.CanvasNet.Mermaid.Cose;
using DemaConsulting.CanvasNet.Mermaid.Dagre;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Cose;

/// <summary>
///     Subsystem tests of the Cose subsystem: whole layouts of representative trees, their determinism, the edge points
///     that come with them, and the failures and limits of hostile sizes.
/// </summary>
public class CoseTests
{
    /// <summary>
    ///     Validates that the same tree gives the same layout, bit for bit, every time and on parallel threads (the layout
    ///     has no random source and no shared state).
    /// </summary>
    [Fact]
    public void Cose_Layout_SameTree_Deterministic()
    {
        // Arrange
        var (sizes, links) = MindmapLike(40);
        var first = CoseLayout.Run(sizes, links, Meter());

        // Act
        var others = Enumerable.Range(0, 4)
            .AsParallel()
            .Select(_ => CoseLayout.Run(sizes, links, Meter()))
            .ToArray();

        // Assert
        foreach (var other in others)
        {
            Assert.Equal(first.Centers, other.Centers);
            Assert.Equal(first.Edges, other.Edges);
            Assert.Equal(first.Iterations, other.Iterations);
        }
    }

    /// <summary>
    ///     Validates that a mindmap-like tree is laid out with every node inside the drawing (the top-left corner at the
    ///     margin), edges whose ends lie 15 pixels from their node centers, and middle points between the ends.
    /// </summary>
    [Fact]
    public void Cose_Layout_MindmapTree_EdgesBetweenCenters()
    {
        // Arrange
        var (sizes, links) = MindmapLike(25);

        // Act
        var result = CoseLayout.Run(sizes, links, Meter());

        // Assert: the drawing starts at the margin of 15
        var left = result.Centers.Select((c, i) => c.X - (sizes[i].Width / 2)).Min();
        var top = result.Centers.Select((c, i) => c.Y - (sizes[i].Height / 2)).Min();
        Assert.Equal(15, left, 9);
        Assert.Equal(15, top, 9);

        // Every edge runs between its node centers, 15 pixels from each
        for (var i = 0; i < links.Length; i++)
        {
            var points = result.Edges[i];
            var source = result.Centers[links[i].Source];
            var target = result.Centers[links[i].Target];
            Assert.Equal(15, Distance(points.Start, source), 9);
            Assert.Equal(15, Distance(points.End, target), 9);
            Assert.Equal((points.Start.X + points.End.X) / 2, points.Mid.X, 9);
            Assert.Equal((points.Start.Y + points.End.Y) / 2, points.Mid.Y, 9);
        }
    }

    /// <summary>
    ///     Validates that nodes that all start overlapping (identical sizes and zero sizes) are pushed apart into a
    ///     finite layout.
    /// </summary>
    [Fact]
    public void Cose_Layout_OverlappingStart_FiniteLayout()
    {
        // Arrange
        var links = Enumerable.Range(1, 11).Select(i => new CoseLink((i - 1) / 2, i)).ToArray();
        var same = Enumerable.Repeat(new CoseSize(100, 44), 12).ToArray();
        var zero = Enumerable.Repeat(new CoseSize(0, 0), 12).ToArray();

        // Act
        var sameResult = CoseLayout.Run(same, links, Meter());
        var zeroResult = CoseLayout.Run(zero, links, Meter());

        // Assert
        Assert.All(sameResult.Centers.Concat(zeroResult.Centers), c => Assert.True(double.IsFinite(c.X) && double.IsFinite(c.Y)));
        Assert.Equal(sameResult.Centers.Count, sameResult.Centers.Distinct().Count());
    }

    /// <summary>
    ///     Validates that sizes far beyond any drawing end in a limit or render failure, never a hang: a grid of
    ///     billions of cells is charged before it is made.
    /// </summary>
    [Fact]
    public void Cose_Layout_HugeSizes_LimitOrRenderFailure()
    {
        // Arrange
        var links = new[] { new CoseLink(0, 1) };
        CoseSize[][] cases =
        [
            [new CoseSize(4e6, 3e6), new CoseSize(50, 40)],
            [new CoseSize(1e300, 1e300), new CoseSize(50, 40)],
            [new CoseSize(double.MaxValue, 40), new CoseSize(50, 40)],
        ];

        // Act / Assert
        foreach (var sizes in cases)
        {
            var ex = Assert.Throws<MermaidRenderException>(() => CoseLayout.Run(sizes, links, Meter()));
            Assert.Contains(ex.Code, new[] { MermaidDiagnosticCodes.LimitExceeded, MermaidDiagnosticCodes.RenderFailed });
        }
    }

    /// <summary>
    ///     Validates that a tree of 400 nodes is laid out within the default work limit.
    /// </summary>
    [Fact]
    public void Cose_Layout_FourHundredNodes_WithinDefaultLimit()
    {
        // Arrange
        var (sizes, links) = MindmapLike(400);
        var meter = Meter();

        // Act
        var result = CoseLayout.Run(sizes, links, meter);

        // Assert
        Assert.Equal(400, result.Centers.Count);
        Assert.True(meter.Used < MermaidLimits.Default.MaxLayoutWork);
    }

    /// <summary>
    ///     Creates a meter with the default work limit.
    /// </summary>
    /// <returns>The meter.</returns>
    private static LayoutWorkMeter Meter() =>
        new(MermaidLimits.Default.MaxLayoutWork, default, CancellationToken.None);

    /// <summary>
    ///     Creates a mindmap-like tree: nodes in pre-order, each hanging from one of the last few nodes, with sizes on
    ///     the 1/64-pixel grid of text widths.
    /// </summary>
    /// <param name="count">The number of nodes.</param>
    /// <returns>The sizes and edges.</returns>
    private static (CoseSize[] Sizes, CoseLink[] Links) MindmapLike(int count)
    {
        var sizes = Enumerable.Range(0, count)
            .Select(i => new CoseSize(40 + (((i * 7919) % 6400) / 64.0), 24 + (20 * (i % 3))))
            .ToArray();
        var links = Enumerable.Range(1, count - 1)
            .Select(i => new CoseLink(Math.Max(0, i - 1 - ((i * 31) % Math.Min(i, 4))), i))
            .ToArray();
        return (sizes, links);
    }

    /// <summary>
    ///     Gets the distance between two points.
    /// </summary>
    /// <param name="a">The first point.</param>
    /// <param name="b">The second point.</param>
    /// <returns>The distance.</returns>
    private static double Distance(CosePoint a, CosePoint b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));
}
