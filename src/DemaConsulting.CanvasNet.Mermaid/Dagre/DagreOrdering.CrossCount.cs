// Portions derived from dagre-d3-es (MIT); see third-party/README.md
using LayoutGraph = DemaConsulting.CanvasNet.Mermaid.Graphlib.Graph<
    DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutGraphLabel,
    DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutNode,
    DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutEdge>;

namespace DemaConsulting.CanvasNet.Mermaid.Dagre;

/// <summary>
///     The crossing count (<c>order/cross-count.js</c>).
/// </summary>
internal static partial class DagreOrdering
{
    /// <summary>
    ///     Counts the weighted edge crossings of a layering (dagre's <c>crossCount</c>), with the accumulator tree of
    ///     Barth, Juenger and Mutzel's "Simple and Efficient Bilayer Cross Counting".
    /// </summary>
    /// <param name="g">The layout graph.</param>
    /// <param name="layering">The nodes of each rank in order.</param>
    /// <param name="meter">The work meter.</param>
    /// <returns>The sum over adjacent ranks of the products of the weights of crossing edges.</returns>
    public static double CrossCount(LayoutGraph g, List<List<string>> layering, LayoutWorkMeter meter)
    {
        var cc = 0.0;
        for (var i = 1; i < layering.Count; ++i)
        {
            cc += TwoLayerCrossCount(g, layering[i - 1], layering[i], meter);
        }

        return cc;
    }

    /// <summary>
    ///     Counts the weighted crossings between two adjacent ranks.
    /// </summary>
    /// <param name="g">The layout graph.</param>
    /// <param name="northLayer">The upper rank.</param>
    /// <param name="southLayer">The lower rank.</param>
    /// <param name="meter">The work meter.</param>
    /// <returns>The weighted crossings.</returns>
    private static double TwoLayerCrossCount(LayoutGraph g, List<string> northLayer, List<string> southLayer, LayoutWorkMeter meter)
    {
        // Sort the edges by north position, then by south position
        var southPos = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < southLayer.Count; i++)
        {
            southPos[southLayer[i]] = i;
        }

        var southEntries = new List<(double Pos, double Weight)>();
        foreach (var v in northLayer)
        {
            var entries = g.OutEdges(v)!.Select(e =>
                (Pos: southPos.TryGetValue(e.W, out var pos) ? pos : double.NaN, g.Edge(e)!.Weight));
            southEntries.AddRange(Lodash.SortBy(entries, entry => entry.Pos));
        }

        meter.Charge(northLayer.Count + southLayer.Count + southEntries.Count);

        // Build the accumulator tree
        var firstIndex = 1;
        while (firstIndex < southLayer.Count)
        {
            firstIndex <<= 1;
        }

        var treeSize = (2 * firstIndex) - 1;
        firstIndex -= 1;
        var tree = new double[treeSize];

        // Calculate the weighted crossings
        var cc = 0.0;
        foreach (var (pos, weight) in southEntries)
        {
            var index = (int)pos + firstIndex;
            tree[index] += weight;
            var weightSum = 0.0;
            while (index > 0)
            {
                if (index % 2 != 0)
                {
                    weightSum += tree[index + 1];
                }

                index = (index - 1) >> 1;
                tree[index] += weight;
            }

            cc += weight * weightSum;
        }

        return cc;
    }
}
