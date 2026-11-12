// Portions derived from cytoscape-cose-bilkent, cose-base and layout-base (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Mermaid.Dagre;

namespace DemaConsulting.CanvasNet.Mermaid.Cose;

/// <summary>
///     The size of a node given to the cose layout (Chrome's <c>getBBox</c> of the drawn node in Mermaid).
/// </summary>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
internal readonly record struct CoseSize(double Width, double Height);

/// <summary>
///     An edge given to the cose layout: the indexes of its source and target nodes.
/// </summary>
/// <param name="Source">The index of the source node.</param>
/// <param name="Target">The index of the target node.</param>
internal readonly record struct CoseLink(int Source, int Target);

/// <summary>
///     A point of the cose layout's results.
/// </summary>
/// <param name="X">The x coordinate (NaN where cytoscape gives none).</param>
/// <param name="Y">The y coordinate (NaN where cytoscape gives none).</param>
internal readonly record struct CosePoint(double X, double Y);

/// <summary>
///     The three points cytoscape stores for a straight edge (its <c>rscratch</c> start, middle and end), which Mermaid
///     draws as the edge's route.
/// </summary>
/// <param name="Start">The start, on the circle of radius 15 around the source's center.</param>
/// <param name="Mid">The middle point.</param>
/// <param name="End">The end, on the circle of radius 15 around the target's center.</param>
internal readonly record struct CoseEdgePoints(CosePoint Start, CosePoint Mid, CosePoint End);

/// <summary>
///     The results of the cose layout.
/// </summary>
/// <param name="Centers">The node centers, in the order of the input nodes.</param>
/// <param name="Edges">The edge points, in the order of the input edges.</param>
/// <param name="Iterations">The number of iterations (ticks) the spring embedder ran.</param>
internal sealed record CoseResult(IReadOnlyList<CosePoint> Centers, IReadOnlyList<CoseEdgePoints> Edges, int Iterations);

/// <summary>
///     The cose-bilkent force-directed layout as Mermaid runs it for a mindmap: a port of the subset of layout-base,
///     cose-base and cytoscape-cose-bilkent that a single tree reaches with Mermaid's options, and of the cytoscape
///     straight-edge points Mermaid reads back.
/// </summary>
/// <remarks>
///     <para>
///         Mermaid's cose-bilkent renderer measures every node, adds the nodes (at the origin, with the measured sizes)
///         and the edges to cytoscape, and runs <c>cose-bilkent</c> with <c>quality: 'proof'</c> and <c>animate: false</c>.
///         A mindmap is always one tree, so the layout always takes the same path: <c>getFlatForest</c> finds the tree,
///         <c>positionNodesRadially</c> places it around its center with the radial layout, and the spring embedder
///         moves the nodes (springs along the edges, repulsion between nearby nodes through a grid) under a cooling
///         schedule until it converges or reaches its iteration limit; the drawing is then moved so its top-left corner,
///         less a margin of 15, is at the origin. The tree reduction, the random placement, the tiling of zero-degree
///         nodes and the compound-node code are never reached by a tree, so no random number is ever drawn; they are not
///         ported, and <see cref="Run"/> refuses any input that is not one tree.
///     </para>
///     <para>
///         The layout is chaotic, so the port follows the JavaScript operation order exactly and computes the sines,
///         cosines, logarithms and powers correctly rounded, as Chrome does (see <c>CoseLayout.Math.cs</c>). With the
///         same node sizes it gives Mermaid's positions bit for bit, as the goldens recorded in Chrome check.
///     </para>
///     <para>
///         Every step charges the <see cref="LayoutWorkMeter"/>, which bounds the work and observes cancellation: the
///         input, the forest search, the center search, the radial placement, each iteration's nodes and edges, every
///         repulsion pair, every grid (its cells before it is filled), every grid cell a node is put in or searched, and
///         every transcendental function (one, plus each higher precision it needed). A grid that JavaScript could not
///         build or fill (a <c>RangeError</c> or <c>TypeError</c> in Mermaid) fails with
///         <see cref="MermaidDiagnosticCodes.RenderFailed"/>. An instance serves one call on one thread.
///     </para>
/// </remarks>
internal sealed partial class CoseLayout
{
    /// <summary>The work meter of the diagram.</summary>
    private readonly LayoutWorkMeter _meter;

    /// <summary>The nodes of the root graph, in the order they were added (the input order).</summary>
    private readonly List<CoseNode> _nodes = [];

    /// <summary>The edges of the root graph, in the order they were added (the input order).</summary>
    private readonly List<CoseEdge> _edges = [];

    /// <summary>
    ///     Initializes a layout.
    /// </summary>
    /// <param name="meter">The work meter of the diagram.</param>
    private CoseLayout(LayoutWorkMeter meter) => _meter = meter;

    /// <summary>
    ///     Lays out a tree as Mermaid's cose-bilkent layout does, and computes cytoscape's edge points.
    /// </summary>
    /// <param name="nodes">The node sizes, in the order Mermaid adds the nodes to cytoscape.</param>
    /// <param name="edges">The edges, in the order Mermaid adds them; together with the nodes they must form one tree.</param>
    /// <param name="meter">The work meter of the diagram.</param>
    /// <returns>The node centers and edge points; empty for no nodes.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when the nodes and edges are not one tree (an internal invariant: a mindmap always is one).
    /// </exception>
    /// <exception cref="MermaidRenderException">
    ///     Thrown with <see cref="MermaidDiagnosticCodes.LimitExceeded"/> when the work limit is reached (or the stack runs
    ///     short), or with <see cref="MermaidDiagnosticCodes.RenderFailed"/> when Mermaid's layout would fail.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when the call is canceled.</exception>
    public static CoseResult Run(IReadOnlyList<CoseSize> nodes, IReadOnlyList<CoseLink> edges, LayoutWorkMeter meter)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(edges);
        ArgumentNullException.ThrowIfNull(meter);

        // Check the input before any work: only a tree reaches the ported path
        meter.Charge(nodes.Count + edges.Count + 1);
        CheckTree(nodes.Count, edges);

        try
        {
            var layout = new CoseLayout(meter);
            layout.AddGraph(nodes, edges);
            var iterations = layout.RunLayout();
            return layout.Results(iterations);
        }
        catch (InsufficientExecutionStackException)
        {
            throw meter.LimitExceeded();
        }
    }

    /// <summary>
    ///     Checks that the nodes and edges form one tree: one edge fewer than nodes, every end a node, and every node
    ///     reached from the first one (which also rules out self loops, parallel edges and cycles).
    /// </summary>
    /// <param name="count">The number of nodes.</param>
    /// <param name="edges">The edges.</param>
    /// <exception cref="ArgumentException">Thrown when they do not form one tree.</exception>
    private static void CheckTree(int count, IReadOnlyList<CoseLink> edges)
    {
        if (count == 0 && edges.Count == 0)
        {
            return;
        }

        if (edges.Count != count - 1)
        {
            throw new ArgumentException("the cose layout needs one tree: a tree has one edge fewer than nodes", nameof(edges));
        }

        // Breadth-first search from the first node over the undirected edges
        var neighbors = new List<int>[count];
        for (var i = 0; i < count; i++)
        {
            neighbors[i] = [];
        }

        foreach (var edge in edges)
        {
            if ((uint)edge.Source >= (uint)count || (uint)edge.Target >= (uint)count)
            {
                throw new ArgumentException("the cose layout needs one tree: an edge ends outside the nodes", nameof(edges));
            }

            neighbors[edge.Source].Add(edge.Target);
            neighbors[edge.Target].Add(edge.Source);
        }

        var seen = new bool[count];
        var queue = new Queue<int>();
        seen[0] = true;
        queue.Enqueue(0);
        var reached = 1;
        while (queue.Count > 0)
        {
            foreach (var next in neighbors[queue.Dequeue()])
            {
                if (!seen[next])
                {
                    seen[next] = true;
                    reached++;
                    queue.Enqueue(next);
                }
            }
        }

        if (reached != count)
        {
            throw new ArgumentException("the cose layout needs one tree: the graph is not connected", nameof(edges));
        }
    }

    /// <summary>
    ///     Builds the root graph as cytoscape-cose-bilkent's <c>run</c> and <c>processChildrenList</c> do.
    /// </summary>
    /// <param name="sizes">The node sizes.</param>
    /// <param name="links">The edges.</param>
    private void AddGraph(IReadOnlyList<CoseSize> sizes, IReadOnlyList<CoseLink> links)
    {
        // Each node is a CoSENode with the rectangle (x - w/2, y - h/2, w, h) at Mermaid's position (0, 0);
        // parseFloat of a number keeps it, except that -0 becomes 0; a NaN corner becomes 0
        foreach (var size in sizes)
        {
            var width = ParseFloat(size.Width);
            var height = ParseFloat(size.Height);
            var node = new CoseNode(_nodes.Count)
            {
                X = 0 - (size.Width / 2),
                Y = 0 - (size.Height / 2),
                Width = width,
                Height = height,
            };
            if (double.IsNaN(node.X))
            {
                node.X = 0;
            }

            if (double.IsNaN(node.Y))
            {
                node.Y = 0;
            }

            _nodes.Add(node);
        }

        // Each edge is added to the root graph and to the incidence lists of its ends (a tree has no self loop and no
        // second edge between two nodes, which cose-bilkent would skip)
        foreach (var link in links)
        {
            var edge = new CoseEdge(_nodes[link.Source], _nodes[link.Target]);
            _edges.Add(edge);
            edge.Source.Edges.Add(edge);
            edge.Target.Edges.Add(edge);
        }
    }

    /// <summary>
    ///     Runs the layout (layout-base's <c>Layout.runLayout</c> with cose-base's <c>layout</c> and
    ///     <c>classicLayout</c>).
    /// </summary>
    /// <returns>The number of iterations the spring embedder ran (0 without nodes).</returns>
    private int RunLayout()
    {
        // checkLayoutSuccess fails a graph without nodes: nothing is laid out
        if (_nodes.Count == 0)
        {
            return 0;
        }

        // classicLayout of a graph that is not incremental: a flat forest (here one tree) is placed radially; the
        // weights (noOfChildren), lowest common ancestors, inclusion depths and ideal edge lengths are constants for a
        // flat graph, and a connected graph has no node that gravity applies to
        var tree = FlatForest();
        PositionNodesRadially(tree);

        // The spring embedder, then doPostLayout: transform() moves the drawing to the origin (update() has no view
        // objects to update)
        InitSpringEmbedder();
        var iterations = RunSpringEmbedder();
        Transform(0, 0);
        return iterations;
    }

    /// <summary>
    ///     Reads the results: each node's center (cose-bilkent's <c>getPositions</c>) and each edge's cytoscape points.
    /// </summary>
    /// <param name="iterations">The number of iterations.</param>
    /// <returns>The results.</returns>
    private CoseResult Results(int iterations)
    {
        var centers = new CosePoint[_nodes.Count];
        for (var i = 0; i < _nodes.Count; i++)
        {
            centers[i] = new CosePoint(_nodes[i].CenterX, _nodes[i].CenterY);
        }

        _meter.Charge(_edges.Count + 1);
        var edges = new CoseEdgePoints[_edges.Count];
        for (var i = 0; i < _edges.Count; i++)
        {
            edges[i] = StraightEdgePoints(centers[_edges[i].Source.Index], centers[_edges[i].Target.Index]);
        }

        return new CoseResult(centers, edges, iterations);
    }

    /// <summary>
    ///     Moves the drawing so its top-left corner (less the margin) is at a point (layout-base's
    ///     <c>Layout.transform(newLeftTop)</c>).
    /// </summary>
    /// <param name="left">The new left of the drawing.</param>
    /// <param name="top">The new top of the drawing.</param>
    private void Transform(double left, double top)
    {
        // updateLeftTop gives no corner (null) when no node lies above Integer.MAX_VALUE; then nothing moves
        if (!UpdateLeftTop())
        {
            return;
        }

        var transform = new LayoutTransform(left, top, _left, _top);
        foreach (var node in _nodes)
        {
            node.Transform(transform);
        }
    }

    /// <summary>
    ///     JavaScript's <c>parseFloat</c> of a number: the number itself, except that -0 becomes 0 (its string is "0").
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The parsed number.</returns>
    private static double ParseFloat(double value) => JsEquals(value, 0) ? 0 : value;
}
