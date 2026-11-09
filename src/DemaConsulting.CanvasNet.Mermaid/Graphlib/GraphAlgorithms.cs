// Portions derived from dagre-d3-es (MIT); see third-party/README.md
using System.Runtime.CompilerServices;

namespace DemaConsulting.CanvasNet.Mermaid.Graphlib;

/// <summary>
///     The graphlib depth-first traversals (<c>alg/dfs.js</c>, <c>alg/preorder.js</c> and <c>alg/postorder.js</c> of
///     dagre-d3-es 7.0.14) that the dagre ranker uses; graphlib's other algorithms are not reached by the layout and are
///     not ported.
/// </summary>
/// <remarks>
///     Every traversal follows the graph's key order, so its result is the one graphlib gives. The traversals recurse as
///     graphlib does; the depth is bounded by the number of nodes, which the diagram limits keep small, and a stack that
///     runs short raises <see cref="InsufficientExecutionStackException"/> instead of failing the process.
/// </remarks>
internal static class GraphAlgorithms
{
    /// <summary>
    ///     Lists the nodes reachable from the roots in depth-first preorder (graphlib's <c>alg.preorder</c>).
    /// </summary>
    /// <typeparam name="TGraph">The graph label type.</typeparam>
    /// <typeparam name="TNode">The node label type.</typeparam>
    /// <typeparam name="TEdge">The edge label type.</typeparam>
    /// <param name="g">The graph; successors are followed in a directed graph, neighbors otherwise.</param>
    /// <param name="vs">The roots, visited in order.</param>
    /// <returns>The nodes, each once.</returns>
    /// <exception cref="ArgumentException">Thrown when a root is not a node of the graph.</exception>
    public static List<string> Preorder<TGraph, TNode, TEdge>(Graph<TGraph, TNode, TEdge> g, IEnumerable<string> vs) =>
        Dfs(g, vs, postorder: false);

    /// <summary>
    ///     Lists the nodes reachable from the roots in depth-first postorder (graphlib's <c>alg.postorder</c>).
    /// </summary>
    /// <typeparam name="TGraph">The graph label type.</typeparam>
    /// <typeparam name="TNode">The node label type.</typeparam>
    /// <typeparam name="TEdge">The edge label type.</typeparam>
    /// <param name="g">The graph; successors are followed in a directed graph, neighbors otherwise.</param>
    /// <param name="vs">The roots, visited in order.</param>
    /// <returns>The nodes, each once.</returns>
    /// <exception cref="ArgumentException">Thrown when a root is not a node of the graph.</exception>
    public static List<string> Postorder<TGraph, TNode, TEdge>(Graph<TGraph, TNode, TEdge> g, IEnumerable<string> vs) =>
        Dfs(g, vs, postorder: true);

    /// <summary>
    ///     The depth-first search behind <see cref="Preorder{TGraph,TNode,TEdge}"/> and
    ///     <see cref="Postorder{TGraph,TNode,TEdge}"/> (graphlib's <c>alg.dfs</c>).
    /// </summary>
    /// <typeparam name="TGraph">The graph label type.</typeparam>
    /// <typeparam name="TNode">The node label type.</typeparam>
    /// <typeparam name="TEdge">The edge label type.</typeparam>
    /// <param name="g">The graph.</param>
    /// <param name="vs">The roots.</param>
    /// <param name="postorder">True for postorder, false for preorder.</param>
    /// <returns>The nodes.</returns>
    /// <exception cref="ArgumentException">Thrown when a root is not a node of the graph.</exception>
    private static List<string> Dfs<TGraph, TNode, TEdge>(Graph<TGraph, TNode, TEdge> g, IEnumerable<string> vs, bool postorder)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var acc = new List<string>();
        foreach (var v in vs)
        {
            if (!g.HasNode(v))
            {
                throw new ArgumentException("Graph does not have node: " + v, nameof(vs));
            }

            Visit(v);
        }

        return acc;

        // Visit a node once, recording it before or after its neighbors
        void Visit(string v)
        {
            if (!visited.Add(v))
            {
                return;
            }

            RuntimeHelpers.EnsureSufficientExecutionStack();
            if (!postorder)
            {
                acc.Add(v);
            }

            foreach (var w in (g.IsDirected ? g.Successors(v) : g.Neighbors(v))!)
            {
                Visit(w);
            }

            if (postorder)
            {
                acc.Add(v);
            }
        }
    }
}
