using System.Globalization;
using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Mermaid.Dagre;
using DemaConsulting.CanvasNet.Mermaid.Graphlib;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Dagre;

/// <summary>
///     The dagre stage goldens of <c>Fixtures/upstream-vectors/dagre-layouts.json</c>: graphs from a seeded generator and
///     the layouts the pinned dagre-d3-es computed for them (<c>tools/visual-diff/mermaid/gen-dagre-goldens.mjs</c>).
/// </summary>
internal static class DagreGoldens
{
    /// <summary>The largest difference accepted between two coordinates, relative to their size (at least one).</summary>
    public const double Tolerance = 1e-9;

    /// <summary>The goldens file, read once.</summary>
    private static readonly Lazy<JsonObject> File = new(() =>
        JsonNode.Parse(System.IO.File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "upstream-vectors", "dagre-layouts.json")))!.AsObject());

    /// <summary>
    ///     Gets the layout cases.
    /// </summary>
    public static JsonArray Layouts => File.Value["layouts"]!.AsArray();

    /// <summary>
    ///     Gets the case names, as theory data.
    /// </summary>
    /// <returns>The names.</returns>
    public static TheoryData<string> Names()
    {
        var data = new TheoryData<string>();
        foreach (var layout in Layouts)
        {
            data.Add((string)layout!["name"]!);
        }

        return data;
    }

    /// <summary>
    ///     Gets a case by name.
    /// </summary>
    /// <param name="name">The case name.</param>
    /// <returns>The case.</returns>
    public static JsonObject Case(string name) =>
        Layouts.Select(item => item!.AsObject()).Single(item => (string?)item["name"] == name);

    /// <summary>
    ///     Builds the input graph of a case with the same calls, in the same order, as the generator.
    /// </summary>
    /// <param name="layout">The case.</param>
    /// <returns>The graph.</returns>
    public static Graph<DagreGraph, DagreNode, DagreEdge> Build(JsonObject layout)
    {
        var compound = (bool)layout["compound"]!;
        var g = new Graph<DagreGraph, DagreNode, DagreEdge>(multigraph: (bool)layout["multigraph"]!, compound: compound);
        var graph = layout["graph"]!.AsObject();
        g.SetGraph(new DagreGraph
        {
            RankDir = (string?)graph["rankdir"],
            NodeSep = (double?)graph["nodesep"],
            RankSep = (double?)graph["ranksep"],
            EdgeSep = (double?)graph["edgesep"],
            MarginX = (double?)graph["marginx"],
            MarginY = (double?)graph["marginy"],
            Ranker = (string?)graph["ranker"],
            Align = (string?)graph["align"],
            Acyclicer = (string?)graph["acyclicer"],
        });
        g.SetDefaultEdgeLabel((_, _, _) => new DagreEdge());

        // Nodes, then parents, then edges
        var nodes = layout["nodes"]!.AsArray().Select(node => node!.AsObject()).ToList();
        foreach (var node in nodes)
        {
            g.SetNode((string)node["id"]!, new DagreNode { Width = (double)node["width"]!, Height = (double)node["height"]! });
        }

        if (compound)
        {
            foreach (var node in nodes.Where(node => node["parent"] is not null))
            {
                g.SetParent((string)node["id"]!, (string)node["parent"]!);
            }
        }

        foreach (var edge in layout["edges"]!.AsArray().Select(edge => edge!.AsObject()))
        {
            var label = edge["label"]!.AsObject();
            g.SetEdge((string)edge["v"]!, (string)edge["w"]!, new DagreEdge
            {
                MinLen = (double?)label["minlen"],
                Weight = (double?)label["weight"],
                Width = (double?)label["width"],
                Height = (double?)label["height"],
                LabelOffset = (double?)label["labeloffset"],
                LabelPos = (string?)label["labelpos"],
            }, (string?)edge["name"]);
        }

        return g;
    }

    /// <summary>
    ///     Lays out the graph of a case and compares every result with dagre's.
    /// </summary>
    /// <param name="layout">The case.</param>
    /// <returns>Null when everything matches, else a description of the first difference.</returns>
    public static string? Difference(JsonObject layout)
    {
        var g = Build(layout);
        var expected = layout["expected"]!.AsObject();
        LayoutGraphCheck layoutGraph;
        try
        {
            layoutGraph = new LayoutGraphCheck(DagreLayout.Run(g, new LayoutWorkMeter(long.MaxValue, default, CancellationToken.None)));
        }
        catch (MermaidRenderException ex)
        {
            return expected["error"] is not null ? null : $"layout failed: {ex.Message}";
        }

        if (expected["error"] is { } error)
        {
            return $"dagre fails with '{error}', the port does not";
        }

        return NodeDifference(g, layoutGraph, expected["nodes"]!.AsObject())
            ?? EdgeDifference(g, expected["edges"]!.AsArray())
            ?? Compare("graph width", (double?)expected["width"], g.GraphLabel!.Width)
            ?? Compare("graph height", (double?)expected["height"], g.GraphLabel.Height);
    }

    /// <summary>
    ///     Compares two coordinates within <see cref="Tolerance"/>; null on the JSON side stands for NaN or an infinity.
    /// </summary>
    /// <param name="what">What is compared, for the message.</param>
    /// <param name="expected">The expected value.</param>
    /// <param name="actual">The actual value.</param>
    /// <returns>Null when equal, else the difference.</returns>
    public static string? Compare(string what, double? expected, double actual)
    {
        // JSON writes NaN and the infinities as null
        if (expected is not { } want)
        {
            return double.IsFinite(actual)
                ? string.Create(CultureInfo.InvariantCulture, $"{what}: expected NaN or an infinity, got {actual:R}")
                : null;
        }

        return Math.Abs(want - actual) <= Tolerance * Math.Max(1, Math.Abs(want))
            ? null
            : string.Create(CultureInfo.InvariantCulture, $"{what}: expected {want:R}, got {actual:R}");
    }

    /// <summary>
    ///     Compares the nodes: rank and order exactly, center and size within the tolerance.
    /// </summary>
    /// <param name="g">The laid-out input graph.</param>
    /// <param name="layoutGraph">The internal layout graph.</param>
    /// <param name="expected">The expected nodes.</param>
    /// <returns>Null when equal, else the first difference.</returns>
    private static string? NodeDifference(Graph<DagreGraph, DagreNode, DagreEdge> g, LayoutGraphCheck layoutGraph, JsonObject expected)
    {
        var names = g.Nodes();
        if (names.Length != expected.Count)
        {
            return $"node count: expected {expected.Count}, got {names.Length}";
        }

        foreach (var (v, value) in expected)
        {
            var want = value!.AsObject();
            var node = g.Node(v)!;
            var (rank, order) = layoutGraph.RankAndOrder(v);
            var difference = !Equals((double?)want["rank"], rank)
                ? $"node {v} rank: expected {want["rank"]?.ToJsonString() ?? "null"}, got {rank}"
                : null;
            difference ??= want["order"] is { } wantOrder && (int)wantOrder != order
                ? $"node {v} order: expected {wantOrder}, got {order}"
                : null;
            difference ??= Compare($"node {v} x", (double?)want["x"], node.X)
                ?? Compare($"node {v} y", (double?)want["y"], node.Y)
                ?? Compare($"node {v} width", (double?)want["width"], node.Width ?? 0)
                ?? Compare($"node {v} height", (double?)want["height"], node.Height ?? 0);
            if (difference is not null)
            {
                return difference;
            }
        }

        return null;
    }

    /// <summary>
    ///     Compares the edges in order: route points and label centers within the tolerance.
    /// </summary>
    /// <param name="g">The laid-out input graph.</param>
    /// <param name="expected">The expected edges.</param>
    /// <returns>Null when equal, else the first difference.</returns>
    private static string? EdgeDifference(Graph<DagreGraph, DagreNode, DagreEdge> g, JsonArray expected)
    {
        var edges = g.Edges();
        if (edges.Length != expected.Count)
        {
            return $"edge count: expected {expected.Count}, got {edges.Length}";
        }

        for (var i = 0; i < edges.Length; i++)
        {
            var want = expected[i]!.AsObject();
            var e = edges[i];
            if (e.V != (string)want["v"]! || e.W != (string)want["w"]! || e.Name != (string?)want["name"])
            {
                return $"edge {i}: expected {want["v"]}->{want["w"]} ({want["name"]}), got {e.V}->{e.W} ({e.Name})";
            }

            var label = g.Edge(e)!;
            var points = want["points"]!.AsArray();
            var actualPoints = label.Points ?? [];
            if (points.Count != actualPoints.Count)
            {
                return $"edge {e.V}->{e.W} point count: expected {points.Count}, got {actualPoints.Count}";
            }

            for (var p = 0; p < points.Count; p++)
            {
                var point = points[p]!.AsArray();
                var difference = Compare($"edge {e.V}->{e.W} point {p} x", (double?)point[0], actualPoints[p].X)
                    ?? Compare($"edge {e.V}->{e.W} point {p} y", (double?)point[1], actualPoints[p].Y);
                if (difference is not null)
                {
                    return difference;
                }
            }

            if (want.ContainsKey("x") != (label.X is not null))
            {
                return $"edge {e.V}->{e.W} label: expected {(want.ContainsKey("x") ? "a position" : "none")}";
            }

            if (label.X is { } x)
            {
                var difference = Compare($"edge {e.V}->{e.W} label x", (double?)want["x"], x)
                    ?? Compare($"edge {e.V}->{e.W} label y", (double?)want["y"], label.Y ?? double.NaN);
                if (difference is not null)
                {
                    return difference;
                }
            }
        }

        return null;
    }

    /// <summary>
    ///     Read access to the internal layout graph for the rank and order checks.
    /// </summary>
    /// <param name="graph">The layout graph returned by the layout.</param>
    private sealed class LayoutGraphCheck(Graph<LayoutGraphLabel, LayoutNode, LayoutEdge> graph)
    {
        /// <summary>
        ///     Gets the rank and order of a node.
        /// </summary>
        /// <param name="v">The node.</param>
        /// <returns>The rank (null for a cluster) and the order.</returns>
        public (double? Rank, int Order) RankAndOrder(string v)
        {
            var node = graph.Node(v)!;
            return (node.Rank, node.Order);
        }
    }
}
