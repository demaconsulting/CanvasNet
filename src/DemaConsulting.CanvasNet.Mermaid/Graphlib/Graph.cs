// Portions derived from dagre-d3-es (MIT); see third-party/README.md
namespace DemaConsulting.CanvasNet.Mermaid.Graphlib;

/// <summary>
///     A graph with labels on the graph, its nodes and its edges: a port of graphlib's <c>Graph</c> (dagre-d3-es 7.0.14).
/// </summary>
/// <remarks>
///     <para>
///         The graph is directed by default and may be a multigraph (several named edges between the same nodes) and
///         compound (nodes nested in parent nodes). Every collection keeps the key order of the JavaScript object that
///         graphlib stores it in (<see cref="ObjectKeyMap{TValue}"/>): nodes, children, predecessors and successors list
///         array-index names first in numeric order, then the other names in insertion order; edges are listed in
///         insertion order. Removing and adding an item again moves it to the end, as in JavaScript. dagre's choices
///         depend on this order, so it is part of the contract.
///     </para>
///     <para>
///         A label that was never given is the default label (<see cref="SetDefaultNodeLabel"/>,
///         <see cref="SetDefaultEdgeLabel"/>), which is <c>default</c> unless set. Methods that graphlib answers with
///         <c>undefined</c> for an unknown node return null. Not thread-safe.
///     </para>
/// </remarks>
/// <typeparam name="TGraph">The type of the graph label.</typeparam>
/// <typeparam name="TNode">The type of the node labels.</typeparam>
/// <typeparam name="TEdge">The type of the edge labels.</typeparam>
internal sealed class Graph<TGraph, TNode, TEdge>
{
    /// <summary>The key of the implicit root of a compound graph.</summary>
    private const string GraphNode = "\0";

    /// <summary>The name part of the key of an unnamed edge.</summary>
    private const string DefaultEdgeName = "\0";

    /// <summary>The delimiter of the parts of an edge key.</summary>
    private const string EdgeKeyDelimiter = "\u0001";

    /// <summary>The node labels by node.</summary>
    private readonly ObjectKeyMap<TNode?> _nodes = new();

    /// <summary>The parent of each node of a compound graph (<see cref="GraphNode"/> at the top level).</summary>
    private readonly Dictionary<string, string>? _parent;

    /// <summary>The children of each node of a compound graph, including <see cref="GraphNode"/>.</summary>
    private readonly Dictionary<string, ObjectKeyMap<bool>>? _children;

    /// <summary>The in-edges of each node by edge key.</summary>
    private readonly Dictionary<string, ObjectKeyMap<GraphEdge>> _in = new(StringComparer.Ordinal);

    /// <summary>The number of edges from each predecessor of each node.</summary>
    private readonly Dictionary<string, ObjectKeyMap<int>> _preds = new(StringComparer.Ordinal);

    /// <summary>The out-edges of each node by edge key.</summary>
    private readonly Dictionary<string, ObjectKeyMap<GraphEdge>> _out = new(StringComparer.Ordinal);

    /// <summary>The number of edges to each successor of each node.</summary>
    private readonly Dictionary<string, ObjectKeyMap<int>> _sucs = new(StringComparer.Ordinal);

    /// <summary>The edges and their labels by edge key, in insertion order.</summary>
    private readonly ObjectKeyMap<EdgeEntry> _edges = new();

    /// <summary>The function that gives the label of a node added without one.</summary>
    private Func<string, TNode?> _defaultNodeLabel = _ => default;

    /// <summary>The function that gives the label of an edge added without one (tail, head, name).</summary>
    private Func<string, string, string?, TEdge?> _defaultEdgeLabel = (_, _, _) => default;

    /// <summary>
    ///     Initializes a new graph.
    /// </summary>
    /// <param name="directed">Whether edges have a direction; true by default.</param>
    /// <param name="multigraph">Whether several named edges may join the same nodes; false by default.</param>
    /// <param name="compound">Whether nodes may have parents; false by default.</param>
    public Graph(bool directed = true, bool multigraph = false, bool compound = false)
    {
        IsDirected = directed;
        IsMultigraph = multigraph;
        IsCompound = compound;
        if (compound)
        {
            _parent = new Dictionary<string, string>(StringComparer.Ordinal);
            _children = new Dictionary<string, ObjectKeyMap<bool>>(StringComparer.Ordinal) { [GraphNode] = new() };
        }
    }

    /// <summary>Gets whether edges have a direction.</summary>
    public bool IsDirected { get; }

    /// <summary>Gets whether several named edges may join the same nodes.</summary>
    public bool IsMultigraph { get; }

    /// <summary>Gets whether nodes may have parents.</summary>
    public bool IsCompound { get; }

    /// <summary>Gets or sets the graph label.</summary>
    public TGraph? GraphLabel { get; set; }

    /// <summary>Gets the number of nodes.</summary>
    public int NodeCount => _nodes.Count;

    /// <summary>Gets the number of edges.</summary>
    public int EdgeCount => _edges.Count;

    /// <summary>
    ///     Sets the graph label (graphlib's <c>setGraph</c>).
    /// </summary>
    /// <param name="label">The label.</param>
    /// <returns>This graph.</returns>
    public Graph<TGraph, TNode, TEdge> SetGraph(TGraph? label)
    {
        GraphLabel = label;
        return this;
    }

    /// <summary>
    ///     Sets the function that gives the label of a node added without one.
    /// </summary>
    /// <param name="labelOf">The function of the node name.</param>
    /// <returns>This graph.</returns>
    public Graph<TGraph, TNode, TEdge> SetDefaultNodeLabel(Func<string, TNode?> labelOf)
    {
        _defaultNodeLabel = labelOf;
        return this;
    }

    /// <summary>
    ///     Sets the function that gives the label of an edge added without one.
    /// </summary>
    /// <param name="labelOf">The function of the tail, the head and the name.</param>
    /// <returns>This graph.</returns>
    public Graph<TGraph, TNode, TEdge> SetDefaultEdgeLabel(Func<string, string, string?, TEdge?> labelOf)
    {
        _defaultEdgeLabel = labelOf;
        return this;
    }

    /// <summary>
    ///     Gets the nodes in key order.
    /// </summary>
    /// <returns>The node names.</returns>
    public string[] Nodes() => _nodes.Keys();

    /// <summary>
    ///     Gets the nodes without in-edges, in key order.
    /// </summary>
    /// <returns>The node names.</returns>
    public string[] Sources() => Array.FindAll(Nodes(), v => _in[v].Count == 0);

    /// <summary>
    ///     Gets the nodes without out-edges, in key order.
    /// </summary>
    /// <returns>The node names.</returns>
    public string[] Sinks() => Array.FindAll(Nodes(), v => _out[v].Count == 0);

    /// <summary>
    ///     Adds each node that is not present yet, with the default label.
    /// </summary>
    /// <param name="vs">The node names.</param>
    /// <returns>This graph.</returns>
    public Graph<TGraph, TNode, TEdge> SetNodes(IEnumerable<string> vs)
    {
        foreach (var v in vs)
        {
            SetNode(v);
        }

        return this;
    }

    /// <summary>
    ///     Adds a node with the default label when it is not present; an existing node keeps its label.
    /// </summary>
    /// <param name="v">The node name.</param>
    /// <returns>This graph.</returns>
    public Graph<TGraph, TNode, TEdge> SetNode(string v)
    {
        if (!_nodes.ContainsKey(v))
        {
            AddNode(v, _defaultNodeLabel(v));
        }

        return this;
    }

    /// <summary>
    ///     Adds a node with a label, or replaces the label of an existing node.
    /// </summary>
    /// <param name="v">The node name.</param>
    /// <param name="value">The label.</param>
    /// <returns>This graph.</returns>
    public Graph<TGraph, TNode, TEdge> SetNode(string v, TNode? value)
    {
        if (_nodes.ContainsKey(v))
        {
            _nodes[v] = value;
        }
        else
        {
            AddNode(v, value);
        }

        return this;
    }

    /// <summary>
    ///     Gets the label of a node.
    /// </summary>
    /// <param name="v">The node name.</param>
    /// <returns>The label, or the default when the node is absent.</returns>
    public TNode? Node(string v) => _nodes.TryGetValue(v, out var label) ? label : default;

    /// <summary>
    ///     Gets whether a node is present.
    /// </summary>
    /// <param name="v">The node name.</param>
    /// <returns>True when present.</returns>
    public bool HasNode(string v) => _nodes.ContainsKey(v);

    /// <summary>
    ///     Removes a node with its edges; the children of a removed parent move to the top level.
    /// </summary>
    /// <param name="v">The node name.</param>
    /// <returns>This graph.</returns>
    public Graph<TGraph, TNode, TEdge> RemoveNode(string v)
    {
        if (!_nodes.Remove(v))
        {
            return this;
        }

        // Detach the node from its parent and move its children to the top level
        if (_children is not null)
        {
            RemoveFromParentsChildList(v);
            _parent!.Remove(v);
            foreach (var child in Children(v)!)
            {
                SetParent(child);
            }

            _children.Remove(v);
        }

        // Remove the in-edges, then the out-edges (a self loop is gone from both after the first pass)
        foreach (var e in _in[v].Keys())
        {
            RemoveEdge(_edges[e].Edge);
        }

        _in.Remove(v);
        _preds.Remove(v);
        foreach (var e in _out[v].Keys())
        {
            RemoveEdge(_edges[e].Edge);
        }

        _out.Remove(v);
        _sucs.Remove(v);
        return this;
    }

    /// <summary>
    ///     Moves a node to the top level of a compound graph, adding it when absent.
    /// </summary>
    /// <param name="v">The node name.</param>
    /// <returns>This graph.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the graph is not compound.</exception>
    public Graph<TGraph, TNode, TEdge> SetParent(string v) => SetParent(v, null);

    /// <summary>
    ///     Sets the parent of a node of a compound graph, adding the node and the parent when absent.
    /// </summary>
    /// <param name="v">The node name.</param>
    /// <param name="parent">The parent name, or null for the top level.</param>
    /// <returns>This graph.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the graph is not compound, or when the parent is the node or one of its descendants.
    /// </exception>
    public Graph<TGraph, TNode, TEdge> SetParent(string v, string? parent)
    {
        if (_children is null)
        {
            throw new InvalidOperationException("Cannot set parent in a non-compound graph");
        }

        // Refuse a cycle, then make sure the parent exists
        if (parent is null)
        {
            parent = GraphNode;
        }
        else
        {
            for (var ancestor = parent; ancestor is not null; ancestor = Parent(ancestor))
            {
                if (ancestor == v)
                {
                    throw new InvalidOperationException($"Setting {parent} as parent of {v} would create a cycle");
                }
            }

            SetNode(parent);
        }

        SetNode(v);
        RemoveFromParentsChildList(v);
        _parent![v] = parent;
        _children[parent][v] = true;
        return this;
    }

    /// <summary>
    ///     Gets the parent of a node.
    /// </summary>
    /// <param name="v">The node name.</param>
    /// <returns>The parent name, or null at the top level, for an absent node or in a graph that is not compound.</returns>
    public string? Parent(string v) =>
        _parent is not null && _parent.TryGetValue(v, out var parent) && parent != GraphNode ? parent : null;

    /// <summary>
    ///     Gets the children of a node, or the top-level nodes.
    /// </summary>
    /// <param name="v">The node name, or null for the top level.</param>
    /// <returns>The child names in key order, or null for an absent node.</returns>
    public string[]? Children(string? v = null)
    {
        v ??= GraphNode;
        if (_children is not null)
        {
            return _children.TryGetValue(v, out var children) ? children.Keys() : null;
        }

        if (v == GraphNode)
        {
            return Nodes();
        }

        return HasNode(v) ? [] : null;
    }

    /// <summary>
    ///     Gets the nodes with an edge to a node.
    /// </summary>
    /// <param name="v">The node name.</param>
    /// <returns>The predecessor names in key order, or null for an absent node.</returns>
    public string[]? Predecessors(string v) => _preds.TryGetValue(v, out var preds) ? preds.Keys() : null;

    /// <summary>
    ///     Gets the nodes with an edge from a node.
    /// </summary>
    /// <param name="v">The node name.</param>
    /// <returns>The successor names in key order, or null for an absent node.</returns>
    public string[]? Successors(string v) => _sucs.TryGetValue(v, out var sucs) ? sucs.Keys() : null;

    /// <summary>
    ///     Gets the predecessors then the successors of a node that are not predecessors (lodash's <c>_.union</c>).
    /// </summary>
    /// <param name="v">The node name.</param>
    /// <returns>The neighbor names, or null for an absent node.</returns>
    public string[]? Neighbors(string v)
    {
        var preds = Predecessors(v);
        if (preds is null)
        {
            return null;
        }

        // lodash's _.union keeps the first occurrence of each name
        return [.. preds.Concat(Successors(v)!).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>
    ///     Gets whether a node has no successors (directed) or no neighbors (undirected).
    /// </summary>
    /// <param name="v">The node name; must be present.</param>
    /// <returns>True for a leaf.</returns>
    public bool IsLeaf(string v) => (IsDirected ? Successors(v) : Neighbors(v))!.Length == 0;

    /// <summary>
    ///     Copies the graph with the nodes that pass a filter, the edges between them, and each node under its nearest
    ///     ancestor that passes.
    /// </summary>
    /// <param name="filter">The node filter.</param>
    /// <returns>The new graph, which shares the labels.</returns>
    public Graph<TGraph, TNode, TEdge> FilterNodes(Func<string, bool> filter)
    {
        var copy = new Graph<TGraph, TNode, TEdge>(IsDirected, IsMultigraph, IsCompound);
        copy.SetGraph(GraphLabel);

        // Nodes, then edges whose ends are kept
        foreach (var v in _nodes.Keys().Where(filter))
        {
            copy.SetNode(v, _nodes[v]);
        }

        foreach (var entry in _edges.Values().Where(entry => copy.HasNode(entry.Edge.V) && copy.HasNode(entry.Edge.W)))
        {
            copy.SetEdge(entry.Edge, entry.Label);
        }

        // Each kept node goes under its nearest kept ancestor (graphlib's memoized findParent)
        if (IsCompound)
        {
            var parents = new Dictionary<string, string?>(StringComparer.Ordinal);
            string? FindParent(string v)
            {
                var parent = Parent(v);
                if (parent is null || copy.HasNode(parent))
                {
                    parents[v] = parent;
                    return parent;
                }

                return parents.TryGetValue(parent, out var known) ? known : FindParent(parent);
            }

            foreach (var v in copy.Nodes())
            {
                copy.SetParent(v, FindParent(v));
            }
        }

        return copy;
    }

    /// <summary>
    ///     Gets the edges in insertion order.
    /// </summary>
    /// <returns>The edges.</returns>
    public GraphEdge[] Edges() => Array.ConvertAll(_edges.Values(), entry => entry.Edge);

    /// <summary>
    ///     Adds an edge between each pair of consecutive nodes of a path, with the default label.
    /// </summary>
    /// <param name="vs">The nodes of the path.</param>
    /// <returns>This graph.</returns>
    public Graph<TGraph, TNode, TEdge> SetPath(IReadOnlyList<string> vs)
    {
        for (var i = 1; i < vs.Count; i++)
        {
            SetEdge(vs[i - 1], vs[i]);
        }

        return this;
    }

    /// <summary>
    ///     Adds an edge with a label between each pair of consecutive nodes of a path.
    /// </summary>
    /// <param name="vs">The nodes of the path.</param>
    /// <param name="value">The label of every edge.</param>
    /// <returns>This graph.</returns>
    public Graph<TGraph, TNode, TEdge> SetPath(IReadOnlyList<string> vs, TEdge? value)
    {
        for (var i = 1; i < vs.Count; i++)
        {
            SetEdge(vs[i - 1], vs[i], value);
        }

        return this;
    }

    /// <summary>
    ///     Adds an unnamed edge with the default label when absent; an existing edge keeps its label.
    /// </summary>
    /// <param name="v">The tail.</param>
    /// <param name="w">The head.</param>
    /// <returns>This graph.</returns>
    public Graph<TGraph, TNode, TEdge> SetEdge(string v, string w) => SetEdgeCore(v, w, null, default, false);

    /// <summary>
    ///     Adds an edge with a label, or replaces the label of an existing edge; the ends are added when absent.
    /// </summary>
    /// <param name="v">The tail.</param>
    /// <param name="w">The head.</param>
    /// <param name="value">The label.</param>
    /// <param name="name">The edge name, or null; only a multigraph accepts a name.</param>
    /// <returns>This graph.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a new named edge is added to a simple graph.</exception>
    public Graph<TGraph, TNode, TEdge> SetEdge(string v, string w, TEdge? value, string? name = null) =>
        SetEdgeCore(v, w, name, value, true);

    /// <summary>
    ///     Adds an edge with the default label when absent; an existing edge keeps its label.
    /// </summary>
    /// <param name="e">The edge.</param>
    /// <returns>This graph.</returns>
    public Graph<TGraph, TNode, TEdge> SetEdge(GraphEdge e) => SetEdgeCore(e.V, e.W, e.Name, default, false);

    /// <summary>
    ///     Adds an edge with a label, or replaces the label of an existing edge.
    /// </summary>
    /// <param name="e">The edge.</param>
    /// <param name="value">The label.</param>
    /// <returns>This graph.</returns>
    public Graph<TGraph, TNode, TEdge> SetEdge(GraphEdge e, TEdge? value) => SetEdgeCore(e.V, e.W, e.Name, value, true);

    /// <summary>
    ///     Gets the label of an edge.
    /// </summary>
    /// <param name="v">The tail.</param>
    /// <param name="w">The head.</param>
    /// <param name="name">The edge name, or null.</param>
    /// <returns>The label, or the default when the edge is absent.</returns>
    public TEdge? Edge(string v, string w, string? name = null) =>
        _edges.TryGetValue(EdgeId(v, w, name), out var entry) ? entry.Label : default;

    /// <summary>
    ///     Gets the label of an edge.
    /// </summary>
    /// <param name="e">The edge.</param>
    /// <returns>The label, or the default when the edge is absent.</returns>
    public TEdge? Edge(GraphEdge e) => Edge(e.V, e.W, e.Name);

    /// <summary>
    ///     Gets whether an edge is present.
    /// </summary>
    /// <param name="v">The tail.</param>
    /// <param name="w">The head.</param>
    /// <param name="name">The edge name, or null.</param>
    /// <returns>True when present.</returns>
    public bool HasEdge(string v, string w, string? name = null) => _edges.ContainsKey(EdgeId(v, w, name));

    /// <summary>
    ///     Gets whether an edge is present.
    /// </summary>
    /// <param name="e">The edge.</param>
    /// <returns>True when present.</returns>
    public bool HasEdge(GraphEdge e) => HasEdge(e.V, e.W, e.Name);

    /// <summary>
    ///     Removes an edge when present.
    /// </summary>
    /// <param name="v">The tail.</param>
    /// <param name="w">The head.</param>
    /// <param name="name">The edge name, or null.</param>
    /// <returns>This graph.</returns>
    public Graph<TGraph, TNode, TEdge> RemoveEdge(string v, string w, string? name = null)
    {
        var id = EdgeId(v, w, name);
        if (_edges.TryGetValue(id, out var entry))
        {
            var edge = entry.Edge;
            _edges.Remove(id);
            DecrementOrRemoveEntry(_preds[edge.W], edge.V);
            DecrementOrRemoveEntry(_sucs[edge.V], edge.W);
            _in[edge.W].Remove(id);
            _out[edge.V].Remove(id);
        }

        return this;
    }

    /// <summary>
    ///     Removes an edge when present.
    /// </summary>
    /// <param name="e">The edge.</param>
    /// <returns>This graph.</returns>
    public Graph<TGraph, TNode, TEdge> RemoveEdge(GraphEdge e) => RemoveEdge(e.V, e.W, e.Name);

    /// <summary>
    ///     Gets the edges into a node, optionally only those from one tail.
    /// </summary>
    /// <param name="v">The node name.</param>
    /// <param name="u">The tail to keep, or null (or empty) for all.</param>
    /// <returns>The edges in insertion order, or null for an absent node.</returns>
    public GraphEdge[]? InEdges(string v, string? u = null)
    {
        if (!_in.TryGetValue(v, out var inV))
        {
            return null;
        }

        var edges = inV.Values();
        return string.IsNullOrEmpty(u) ? edges : Array.FindAll(edges, edge => edge.V == u);
    }

    /// <summary>
    ///     Gets the edges out of a node, optionally only those to one head.
    /// </summary>
    /// <param name="v">The node name.</param>
    /// <param name="w">The head to keep, or null (or empty) for all.</param>
    /// <returns>The edges in insertion order, or null for an absent node.</returns>
    public GraphEdge[]? OutEdges(string v, string? w = null)
    {
        if (!_out.TryGetValue(v, out var outV))
        {
            return null;
        }

        var edges = outV.Values();
        return string.IsNullOrEmpty(w) ? edges : Array.FindAll(edges, edge => edge.W == w);
    }

    /// <summary>
    ///     Gets the in-edges then the out-edges of a node, optionally only those to or from another node.
    /// </summary>
    /// <param name="v">The node name.</param>
    /// <param name="w">The other end to keep, or null for all.</param>
    /// <returns>The edges, or null for an absent node.</returns>
    public GraphEdge[]? NodeEdges(string v, string? w = null)
    {
        var inEdges = InEdges(v, w);
        return inEdges is null ? null : [.. inEdges, .. OutEdges(v, w)!];
    }

    /// <summary>
    ///     Decrements a count, removing its key at zero.
    /// </summary>
    /// <param name="map">The counts.</param>
    /// <param name="k">The key.</param>
    private static void DecrementOrRemoveEntry(ObjectKeyMap<int> map, string k)
    {
        var count = map[k] - 1;
        if (count == 0)
        {
            map.Remove(k);
        }
        else
        {
            map[k] = count;
        }
    }

    /// <summary>
    ///     Increments a count, adding its key at one.
    /// </summary>
    /// <param name="map">The counts.</param>
    /// <param name="k">The key.</param>
    private static void IncrementOrInitEntry(ObjectKeyMap<int> map, string k) =>
        map[k] = map.TryGetValue(k, out var count) ? count + 1 : 1;

    /// <summary>
    ///     Adds a node that is absent.
    /// </summary>
    /// <param name="v">The node name.</param>
    /// <param name="value">The label.</param>
    private void AddNode(string v, TNode? value)
    {
        _nodes[v] = value;
        if (_children is not null)
        {
            _parent![v] = GraphNode;
            _children[v] = new ObjectKeyMap<bool>();
            _children[GraphNode][v] = true;
        }

        _in[v] = new ObjectKeyMap<GraphEdge>();
        _preds[v] = new ObjectKeyMap<int>();
        _out[v] = new ObjectKeyMap<GraphEdge>();
        _sucs[v] = new ObjectKeyMap<int>();
    }

    /// <summary>
    ///     Removes a node from the child list of its parent.
    /// </summary>
    /// <param name="v">The node name.</param>
    private void RemoveFromParentsChildList(string v) => _children![_parent![v]].Remove(v);

    /// <summary>
    ///     Adds an edge or replaces its label (graphlib's <c>setEdge</c>).
    /// </summary>
    /// <param name="v">The tail.</param>
    /// <param name="w">The head.</param>
    /// <param name="name">The edge name, or null.</param>
    /// <param name="value">The label when <paramref name="valueSpecified"/>.</param>
    /// <param name="valueSpecified">Whether a label was given.</param>
    /// <returns>This graph.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a new named edge is added to a simple graph.</exception>
    private Graph<TGraph, TNode, TEdge> SetEdgeCore(string v, string w, string? name, TEdge? value, bool valueSpecified)
    {
        var id = EdgeId(v, w, name);
        if (_edges.TryGetValue(id, out var existing))
        {
            if (valueSpecified)
            {
                existing.Label = value;
            }

            return this;
        }

        if (name is not null && !IsMultigraph)
        {
            throw new InvalidOperationException("Cannot set a named edge when isMultigraph = false");
        }

        // The ends are added first; the label is the given one or the default
        SetNode(v);
        SetNode(w);
        var label = valueSpecified ? value : _defaultEdgeLabel(v, w, name);
        if (!IsDirected && string.CompareOrdinal(v, w) > 0)
        {
            (v, w) = (w, v);
        }

        var edge = new GraphEdge(v, w, string.IsNullOrEmpty(name) ? null : name);
        _edges[id] = new EdgeEntry(edge, label);
        IncrementOrInitEntry(_preds[w], v);
        IncrementOrInitEntry(_sucs[v], w);
        _in[w][id] = edge;
        _out[v][id] = edge;
        return this;
    }

    /// <summary>
    ///     Gets the key of an edge: the ends (ordered for an undirected graph) and the name, delimited.
    /// </summary>
    /// <param name="v">The tail.</param>
    /// <param name="w">The head.</param>
    /// <param name="name">The edge name, or null.</param>
    /// <returns>The key.</returns>
    private string EdgeId(string v, string w, string? name)
    {
        if (!IsDirected && string.CompareOrdinal(v, w) > 0)
        {
            (v, w) = (w, v);
        }

        return string.Concat(v, EdgeKeyDelimiter, w, EdgeKeyDelimiter, name ?? DefaultEdgeName);
    }

    /// <summary>
    ///     An edge and its label.
    /// </summary>
    /// <param name="edge">The edge.</param>
    /// <param name="label">The label.</param>
    private sealed class EdgeEntry(GraphEdge edge, TEdge? label)
    {
        /// <summary>Gets the edge.</summary>
        public GraphEdge Edge { get; } = edge;

        /// <summary>Gets or sets the label.</summary>
        public TEdge? Label { get; set; } = label;
    }
}
