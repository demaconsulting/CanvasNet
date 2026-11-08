// Portions derived from dagre-d3-es (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Mermaid.Graphlib;
using LayerGraph = DemaConsulting.CanvasNet.Mermaid.Graphlib.Graph<
    DemaConsulting.CanvasNet.Mermaid.Dagre.DagreOrdering.LayerGraphLabel,
    DemaConsulting.CanvasNet.Mermaid.Dagre.DagreOrdering.LayerNode,
    DemaConsulting.CanvasNet.Mermaid.Dagre.DagreOrdering.LayerEdge>;

namespace DemaConsulting.CanvasNet.Mermaid.Dagre;

/// <summary>
///     The cluster constraints (<c>order/add-subgraph-constraints.js</c>).
/// </summary>
internal static partial class DagreOrdering
{
    /// <summary>
    ///     Records, for the order just found, that each cluster comes after the sibling cluster met before it (dagre's
    ///     <c>addSubgraphConstraints</c>), so that later ranks keep the clusters in the same relative order.
    /// </summary>
    /// <param name="g">The layer graph.</param>
    /// <param name="cg">The constraint graph; edges are added.</param>
    /// <param name="vs">The nodes in order.</param>
    public static void AddSubgraphConstraints(LayerGraph g, Graph<object, object, object> cg, List<string> vs)
    {
        var prev = new Dictionary<string, string>(StringComparer.Ordinal);
        string? rootPrev = null;
        foreach (var v in vs)
        {
            var child = g.Parent(v);
            while (!string.IsNullOrEmpty(child))
            {
                var parent = g.Parent(child);
                string? prevChild;
                if (!string.IsNullOrEmpty(parent))
                {
                    prevChild = prev.GetValueOrDefault(parent);
                    prev[parent] = child;
                }
                else
                {
                    prevChild = rootPrev;
                    rootPrev = child;
                }

                if (!string.IsNullOrEmpty(prevChild) && prevChild != child)
                {
                    cg.SetEdge(prevChild, child);
                    break;
                }

                child = parent;
            }
        }
    }
}
