// Portions derived from dagre-d3-es (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Mermaid.Graphlib;
using LayoutGraph = DemaConsulting.CanvasNet.Mermaid.Graphlib.Graph<
    DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutGraphLabel,
    DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutNode,
    DemaConsulting.CanvasNet.Mermaid.Dagre.LayoutEdge>;

namespace DemaConsulting.CanvasNet.Mermaid.Dagre;

/// <summary>
///     The shared helpers of dagre's stages (<c>util.js</c>).
/// </summary>
internal static partial class DagreLayout
{
    /// <summary>
    ///     Adds a dummy node with a new unique name (dagre's <c>addDummyNode</c>).
    /// </summary>
    /// <param name="g">The layout graph.</param>
    /// <param name="meter">The meter, which gives the names.</param>
    /// <param name="type">The dummy kind.</param>
    /// <param name="attrs">The label, whose <see cref="LayoutNode.Dummy"/> is set.</param>
    /// <param name="name">The name prefix.</param>
    /// <returns>The node name.</returns>
    public static string AddDummyNode(LayoutGraph g, LayoutWorkMeter meter, string type, LayoutNode attrs, string name)
    {
        string v;
        do
        {
            v = meter.UniqueId(name);
        }
        while (g.HasNode(v));

        attrs.Dummy = type;
        g.SetNode(v, attrs);
        return v;
    }

    /// <summary>
    ///     Adds a border dummy node of zero size (dagre's <c>addBorderNode</c>).
    /// </summary>
    /// <param name="g">The layout graph.</param>
    /// <param name="meter">The meter, which gives the names.</param>
    /// <param name="prefix">The name prefix.</param>
    /// <returns>The node name.</returns>
    public static string AddBorderNode(LayoutGraph g, LayoutWorkMeter meter, string prefix) =>
        AddDummyNode(g, meter, "border", new LayoutNode(), prefix);

    /// <summary>
    ///     Collapses the parallel edges of a multigraph into one edge with the summed weight and the greatest minimum
    ///     length (dagre's <c>simplify</c>).
    /// </summary>
    /// <param name="g">The graph.</param>
    /// <returns>A simple graph that shares the graph and node labels.</returns>
    public static LayoutGraph Simplify(LayoutGraph g)
    {
        var simplified = new LayoutGraph().SetGraph(g.GraphLabel);
        foreach (var v in g.Nodes())
        {
            simplified.SetNode(v, g.Node(v));
        }

        foreach (var e in g.Edges())
        {
            var simpleLabel = simplified.Edge(e.V, e.W) ?? new LayoutEdge { Weight = 0, MinLen = 1 };
            var label = g.Edge(e)!;
            simplified.SetEdge(e.V, e.W, new LayoutEdge
            {
                Weight = simpleLabel.Weight + label.Weight,
                MinLen = Math.Max(simpleLabel.MinLen, label.MinLen),
            });
        }

        return simplified;
    }

    /// <summary>
    ///     Copies a compound graph without its clusters (dagre's <c>asNonCompoundGraph</c>).
    /// </summary>
    /// <param name="g">The graph.</param>
    /// <returns>A flat graph with the leaves and every edge, sharing all labels.</returns>
    public static LayoutGraph AsNonCompoundGraph(LayoutGraph g)
    {
        var simplified = new LayoutGraph(multigraph: g.IsMultigraph).SetGraph(g.GraphLabel);
        foreach (var v in g.Nodes())
        {
            if (g.Children(v)!.Length == 0)
            {
                simplified.SetNode(v, g.Node(v));
            }
        }

        foreach (var e in g.Edges())
        {
            simplified.SetEdge(e, g.Edge(e));
        }

        return simplified;
    }

    /// <summary>
    ///     Finds where the line from the center of a rectangle to a point leaves the rectangle (dagre's
    ///     <c>intersectRect</c>).
    /// </summary>
    /// <param name="x">The center x.</param>
    /// <param name="y">The center y.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="point">The point outside.</param>
    /// <returns>The intersection.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the point is the center (or not a number).</exception>
    public static DagrePoint IntersectRect(double x, double y, double width, double height, DagrePoint point)
    {
        // Rectangle intersection algorithm from http://math.stackexchange.com/questions/108113/find-edge-between-two-boxes
        var dx = point.X - x;
        var dy = point.Y - y;
        var w = width / 2;
        var h = height / 2;
        if (!Truthy(dx) && !Truthy(dy))
        {
            throw new InvalidOperationException("Not possible to find intersection inside of the rectangle");
        }

        double sx;
        double sy;
        if (Math.Abs(dy) * w > Math.Abs(dx) * h)
        {
            // Intersection is top or bottom of rect
            if (dy < 0)
            {
                h = -h;
            }

            sx = h * dx / dy;
            sy = h;
        }
        else
        {
            // Intersection is left or right of rect
            if (dx < 0)
            {
                w = -w;
            }

            sx = w;
            sy = w * dy / dx;
        }

        return new DagrePoint(x + sx, y + sy);
    }

    /// <summary>
    ///     Lists the ranked nodes of each rank by order (dagre's <c>buildLayerMatrix</c>).
    /// </summary>
    /// <param name="g">The layout graph.</param>
    /// <returns>The layers, from rank 0 to the greatest rank.</returns>
    public static List<List<string>> BuildLayerMatrix(LayoutGraph g)
    {
        var count = MaxRank(g) is { } max && !double.IsNaN(max) ? (int)Math.Max(Math.Ceiling(max + 1), 0) : 0;
        var layering = new List<List<string>>(count);
        for (var i = 0; i < count; i++)
        {
            layering.Add([]);
        }

        foreach (var v in g.Nodes())
        {
            var node = g.Node(v)!;
            if (node.Rank is { } rank)
            {
                var layer = layering[(int)rank];
                while (layer.Count <= node.Order)
                {
                    layer.Add(null!);
                }

                layer[node.Order] = v;
            }
        }

        return layering;
    }

    /// <summary>
    ///     Shifts the ranks so that the least is zero (dagre's <c>normalizeRanks</c>).
    /// </summary>
    /// <param name="g">The layout graph.</param>
    public static void NormalizeRanks(LayoutGraph g)
    {
        var nodes = g.Nodes();
        var min = Lodash.Num(Lodash.Min(nodes.Select(v => g.Node(v)!.Rank)));
        foreach (var v in nodes)
        {
            var node = g.Node(v)!;
            if (node.Rank is { } rank)
            {
                node.Rank = rank - min;
            }
        }
    }

    /// <summary>
    ///     Removes the empty ranks that are not multiples of the nesting rank factor (dagre's <c>removeEmptyRanks</c>).
    /// </summary>
    /// <remarks>
    ///     As in JavaScript, only whole non-negative ranks are layers: a cluster (no rank) and an edge-label proxy at a
    ///     half rank are neither counted nor shifted.
    /// </remarks>
    /// <param name="g">The layout graph.</param>
    public static void RemoveEmptyRanks(LayoutGraph g)
    {
        // Ranks may not start at 0, so they are offset
        var nodes = g.Nodes();
        var offset = Lodash.Num(Lodash.Min(nodes.Select(v => g.Node(v)!.Rank)));
        var layers = new Dictionary<long, List<string>>();
        long length = 0;
        foreach (var v in nodes)
        {
            var rank = Lodash.Num(g.Node(v)!.Rank) - offset;
            if (IsArrayIndex(rank))
            {
                var index = (long)rank;
                if (!layers.TryGetValue(index, out var layer))
                {
                    layer = [];
                    layers[index] = layer;
                }

                layer.Add(v);
                length = Math.Max(length, index + 1);
            }
        }

        // Walk every index, holes included, as lodash's forEach does on a sparse array
        var delta = 0.0;
        var nodeRankFactor = Lodash.Num(g.GraphLabel!.NodeRankFactor);
        for (long i = 0; i < length; i++)
        {
            var present = layers.TryGetValue(i, out var vs);
            if (!present && i % nodeRankFactor != 0)
            {
                --delta;
            }
            else if (Truthy(delta) && present)
            {
                foreach (var v in vs!)
                {
                    var node = g.Node(v)!;
                    node.Rank = Lodash.Num(node.Rank) + delta;
                }
            }
        }
    }

    /// <summary>
    ///     Gets the greatest rank (dagre's <c>maxRank</c>).
    /// </summary>
    /// <param name="g">The layout graph.</param>
    /// <returns>The greatest rank, or null when no node is ranked.</returns>
    public static double? MaxRank(LayoutGraph g) => Lodash.Max(g.Nodes().Select(v => g.Node(v)!.Rank));

    /// <summary>
    ///     Gets the truth value of a JavaScript number: false for zero and NaN.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The truth value.</returns>
    public static bool Truthy(double value) => value != 0 && !double.IsNaN(value);

    /// <summary>
    ///     Gets the truth value of a JavaScript number that may be <c>undefined</c>.
    /// </summary>
    /// <param name="value">The number, or null.</param>
    /// <returns>The truth value.</returns>
    public static bool Truthy(double? value) => value is { } number && Truthy(number);

    /// <summary>
    ///     Gets whether two numbers are equal as JavaScript's <c>===</c> compares them (NaN is never equal, both zeros
    ///     are).
    /// </summary>
    /// <param name="a">The first number.</param>
    /// <param name="b">The second number.</param>
    /// <returns>Whether they are equal.</returns>
    public static bool Same(double a, double b) => !double.IsNaN(a) && !double.IsNaN(b) && a.CompareTo(b) == 0;

    /// <summary>
    ///     Gets whether a number is an array index of a JavaScript array (a whole number from 0 to 2<sup>32</sup> - 2).
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>True when it is.</returns>
    private static bool IsArrayIndex(double value) =>
        value >= 0 && value <= uint.MaxValue - 1 && Same(Math.Floor(value), value);
}
