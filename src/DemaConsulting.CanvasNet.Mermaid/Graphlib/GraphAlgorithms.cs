// Portions derived from dagre-d3-es (MIT); see third-party/README.md
using System.Runtime.CompilerServices;

namespace DemaConsulting.CanvasNet.Mermaid.Graphlib;

/// <summary>
///     The graphlib traversals (<c>alg/*.js</c> of dagre-d3-es 7.0.14) that dagre and Mermaid's dagre wrapper use.
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
    ///     Lists the connected components, ignoring edge direction (graphlib's <c>alg.components</c>).
    /// </summary>
    /// <typeparam name="TGraph">The graph label type.</typeparam>
    /// <typeparam name="TNode">The node label type.</typeparam>
    /// <typeparam name="TEdge">The edge label type.</typeparam>
    /// <param name="g">The graph.</param>
    /// <returns>The components, each a list of nodes in visit order.</returns>
    public static List<List<string>> Components<TGraph, TNode, TEdge>(Graph<TGraph, TNode, TEdge> g)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var components = new List<List<string>>();
        foreach (var v in g.Nodes())
        {
            var component = new List<string>();
            Visit(v, component);
            if (component.Count > 0)
            {
                components.Add(component);
            }
        }

        return components;

        // Successors first, then predecessors, as graphlib walks them
        void Visit(string v, List<string> component)
        {
            if (!visited.Add(v))
            {
                return;
            }

            RuntimeHelpers.EnsureSufficientExecutionStack();
            component.Add(v);
            foreach (var w in g.Successors(v)!)
            {
                Visit(w, component);
            }

            foreach (var w in g.Predecessors(v)!)
            {
                Visit(w, component);
            }
        }
    }

    /// <summary>
    ///     Lists the strongly connected components with Tarjan's algorithm (graphlib's <c>alg.tarjan</c>).
    /// </summary>
    /// <typeparam name="TGraph">The graph label type.</typeparam>
    /// <typeparam name="TNode">The node label type.</typeparam>
    /// <typeparam name="TEdge">The edge label type.</typeparam>
    /// <param name="g">The graph.</param>
    /// <returns>The components in completion order, each a list of nodes in stack-pop order.</returns>
    public static List<List<string>> Tarjan<TGraph, TNode, TEdge>(Graph<TGraph, TNode, TEdge> g)
    {
        var index = 0;
        var stack = new Stack<string>();
        var visited = new Dictionary<string, TarjanEntry>(StringComparer.Ordinal);
        var results = new List<List<string>>();
        foreach (var v in g.Nodes().Where(v => !visited.ContainsKey(v)))
        {
            Visit(v);
        }

        return results;

        // One step of the recursive search: low links from the successors, then pop a component at its root
        void Visit(string v)
        {
            RuntimeHelpers.EnsureSufficientExecutionStack();
            var entry = new TarjanEntry(index);
            visited[v] = entry;
            index++;
            stack.Push(v);
            foreach (var w in g.Successors(v)!)
            {
                if (!visited.TryGetValue(w, out var other))
                {
                    Visit(w);
                    entry.LowLink = Math.Min(entry.LowLink, visited[w].LowLink);
                }
                else if (other.OnStack)
                {
                    entry.LowLink = Math.Min(entry.LowLink, other.Index);
                }
            }

            if (entry.LowLink == entry.Index)
            {
                var component = new List<string>();
                string w;
                do
                {
                    w = stack.Pop();
                    visited[w].OnStack = false;
                    component.Add(w);
                }
                while (v != w);

                results.Add(component);
            }
        }
    }

    /// <summary>
    ///     Lists the cycles: the strongly connected components with more than one node or with a self loop (graphlib's
    ///     <c>alg.findCycles</c>).
    /// </summary>
    /// <typeparam name="TGraph">The graph label type.</typeparam>
    /// <typeparam name="TNode">The node label type.</typeparam>
    /// <typeparam name="TEdge">The edge label type.</typeparam>
    /// <param name="g">The graph.</param>
    /// <returns>The cycles.</returns>
    public static List<List<string>> FindCycles<TGraph, TNode, TEdge>(Graph<TGraph, TNode, TEdge> g) =>
        Tarjan(g).FindAll(component => component.Count > 1 || (component.Count == 1 && g.HasEdge(component[0], component[0])));

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

    /// <summary>
    ///     The search state of one node of <see cref="Tarjan{TGraph,TNode,TEdge}"/>.
    /// </summary>
    /// <param name="index">The visit index, also the first low link.</param>
    private sealed class TarjanEntry(int index)
    {
        /// <summary>Gets the visit index.</summary>
        public int Index { get; } = index;

        /// <summary>Gets or sets the lowest index reachable.</summary>
        public int LowLink { get; set; } = index;

        /// <summary>Gets or sets whether the node is on the stack.</summary>
        public bool OnStack { get; set; } = true;
    }
}
