using System.Globalization;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Tests;

/// <summary>
///     Reads the laid-out elements of a scene by role, in absolute scene coordinates: nodes, subgraphs, edges and edge
///     labels (the facts the reference comparison checks).
/// </summary>
internal static class SceneQuery
{
    /// <summary>
    ///     Lays a document out and returns its scene.
    /// </summary>
    /// <param name="text">The diagram text.</param>
    /// <param name="options">The options, or null.</param>
    /// <returns>The scene.</returns>
    public static Scene Layout(string text, MermaidRenderOptions? options = null) =>
        MermaidDocument.Parse(text).LayoutScene(options, TestContext.Current.CancellationToken);

    /// <summary>
    ///     Gets the drawn nodes: the center (the node group's position) and the box of each, by DOM id.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The nodes.</returns>
    public static Dictionary<string, (double X, double Y, SceneRect Box)> Nodes(Scene scene)
    {
        var nodes = new Dictionary<string, (double X, double Y, SceneRect Box)>(StringComparer.Ordinal);
        foreach (var (item, dx, dy) in scene.Walk())
        {
            if (item is SceneGroup { Role: "node-group" } group && group.Id is { } id)
            {
                var x = dx + group.OffsetX;
                var y = dy + group.OffsetY;
                var box = Scene.BoundsOf(group.Children) ?? new SceneRect(0, 0, 0, 0);
                nodes[id] = (x, y, box.Offset(x, y));
            }
        }

        return nodes;
    }

    /// <summary>
    ///     Gets the subgraph rectangles by subgraph id.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The rectangles.</returns>
    public static Dictionary<string, SceneRect> Clusters(Scene scene)
    {
        var clusters = new Dictionary<string, SceneRect>(StringComparer.Ordinal);
        foreach (var (item, dx, dy) in scene.Walk())
        {
            if (item is ScenePath { Role: "cluster" } path && path.Id is { } id && Scene.BoundsOf([path]) is { } box)
            {
                clusters[id] = box.Offset(dx, dy);
            }
        }

        return clusters;
    }

    /// <summary>
    ///     Gets the inner rectangles of the clusters that have one (a composite state's area under its title) by cluster
    ///     id.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The rectangles.</returns>
    public static Dictionary<string, SceneRect> ClusterInners(Scene scene)
    {
        var inners = new Dictionary<string, SceneRect>(StringComparer.Ordinal);
        foreach (var (item, dx, dy) in scene.Walk())
        {
            if (item is ScenePath { Role: "cluster-inner" } path && path.Id is { } id && Scene.BoundsOf([path]) is { } box)
            {
                inners[id] = box.Offset(dx, dy);
            }
        }

        return inners;
    }

    /// <summary>
    ///     Gets the edge paths by edge id, translated to scene coordinates.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The paths.</returns>
    public static Dictionary<string, ScenePath> Edges(Scene scene)
    {
        var edges = new Dictionary<string, ScenePath>(StringComparer.Ordinal);
        foreach (var (item, dx, dy) in scene.Walk())
        {
            if (item is ScenePath { Role: "edge" } path && path.Id is { } id)
            {
                edges[id] = path with { Segments = [.. path.Segments.Select(s => Translate(s, dx, dy))] };
            }
        }

        return edges;
    }

    /// <summary>
    ///     Gets the edge label centers by edge id: the center of the label's background (its whole box) when it has one,
    ///     else the label's position.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The centers.</returns>
    public static Dictionary<string, (double X, double Y)> EdgeLabels(Scene scene)
    {
        var labels = new Dictionary<string, (double X, double Y)>(StringComparer.Ordinal);
        foreach (var (item, dx, dy) in scene.Walk())
        {
            if (item is SceneGroup { Role: "edge-label" } group && group.Id is { } id)
            {
                var x = dx + group.OffsetX;
                var y = dy + group.OffsetY;
                var background = group.Children.FirstOrDefault(child => child.Role == "edge-label-background");
                labels[id] = background is not null && Scene.BoundsOf([background]) is { } box
                    ? (x + box.X + (box.Width / 2), y + box.Y + (box.Height / 2))
                    : (x, y);
            }
        }

        return labels;
    }

    /// <summary>
    ///     Gets the drawn parts of each node by DOM id, in drawing order and in absolute coordinates, as the extended
    ///     goldens list them: <c>path</c> for an outline, <c>class-annotation</c>, <c>class-label</c>,
    ///     <c>class-member</c> and <c>class-method</c> for the rows of a class box, <c>er-name</c> and
    ///     <c>er-attribute-type</c>, <c>-name</c>, <c>-keys</c> and <c>-comment</c> for the texts of an entity table (an
    ///     empty one as a point where it is placed), <c>divider</c> for the dividers and <c>label</c> for the label of any
    ///     other node (its lines united). Consecutive outlines with the same box are one part (Mermaid draws a rectangle as
    ///     a fill path and a stroke path).
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The parts.</returns>
    public static Dictionary<string, List<(string Kind, SceneRect Box)>> Parts(Scene scene)
    {
        var parts = new Dictionary<string, List<(string Kind, SceneRect Box)>>(StringComparer.Ordinal);
        foreach (var (item, dx, dy) in scene.Walk())
        {
            if (item is not SceneGroup { Role: "node-group" } group || group.Id is not { } id)
            {
                continue;
            }

            var x = dx + group.OffsetX;
            var y = dy + group.OffsetY;
            var list = new List<(string Kind, SceneRect Box)>();
            SceneRect? label = null;
            var labelIndex = -1;
            foreach (var child in group.Children)
            {
                // An empty text of an entity table draws nothing but is compared where it is placed
                var bounds = Scene.BoundsOf([child]);
                if (bounds is null && child is SceneGroup { Role: var cellRole } empty && cellRole.StartsWith("er-", StringComparison.Ordinal))
                {
                    bounds = new SceneRect(empty.OffsetX, empty.OffsetY, 0, 0);
                }

                if (bounds is not { } box)
                {
                    continue;
                }

                box = box.Offset(x, y);
                switch (child)
                {
                    case ScenePath { Role: "node" }:
                        AddPart(list, "path", box);
                        break;
                    case ScenePath { Role: "class-divider" or "state-divider" or "er-divider" }:
                        list.Add(("divider", box));
                        break;
                    case SceneGroup { Role: "class-annotation" or "class-label" or "class-member" or "class-method" } row:
                        list.Add((row.Role, box));
                        break;
                    case SceneGroup { Role: "er-name" or "er-attribute-type" or "er-attribute-name" or "er-attribute-keys" or "er-attribute-comment" } cell:
                        list.Add((cell.Role, box));
                        break;
                    case SceneText { Role: "label" }:
                        if (labelIndex < 0)
                        {
                            labelIndex = list.Count;
                            list.Add(("label", box));
                        }

                        label = label is { } current ? current.Union(box) : box;
                        list[labelIndex] = ("label", label.Value);
                        break;
                    default:
                        break;
                }
            }

            parts[id] = list;
        }

        return parts;
    }

    /// <summary>
    ///     Adds a part, unless it is an outline with the same box as the outline before it.
    /// </summary>
    /// <param name="list">The parts.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="box">The box.</param>
    public static void AddPart(List<(string Kind, SceneRect Box)> list, string kind, SceneRect box)
    {
        if (kind == "path" && list.Count > 0 && list[^1].Kind == "path" && list[^1].Box.Equals(box))
        {
            return;
        }

        list.Add((kind, box));
    }

    /// <summary>
    ///     Gets the positions of the terminal labels (cardinalities) by edge id, in drawing order (the start label, then
    ///     the end label).
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The positions of the labels' groups.</returns>
    public static Dictionary<string, List<(double X, double Y)>> Terminals(Scene scene)
    {
        var terminals = new Dictionary<string, List<(double X, double Y)>>(StringComparer.Ordinal);
        foreach (var (item, dx, dy) in scene.Walk())
        {
            if (item is SceneGroup { Role: "edge-terminal" } group && group.Id is { } id)
            {
                if (!terminals.TryGetValue(id, out var list))
                {
                    terminals[id] = list = [];
                }

                list.Add((dx + group.OffsetX, dy + group.OffsetY));
            }
        }

        return terminals;
    }

    /// <summary>
    ///     Gets the label boxes of the subgraphs by subgraph id: the united boxes of the label lines drawn with the
    ///     subgraph's id outside any node.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="ids">The subgraph ids.</param>
    /// <returns>The boxes.</returns>
    public static Dictionary<string, SceneRect> ClusterLabels(Scene scene, IReadOnlySet<string> ids)
    {
        var labels = new Dictionary<string, SceneRect>(StringComparer.Ordinal);
        foreach (var (item, dx, dy) in scene.Walk())
        {
            if (item is SceneText { Role: "label" } text && text.Id is { } id && ids.Contains(id))
            {
                var box = text.Box.Offset(dx, dy);
                labels[id] = labels.TryGetValue(id, out var current) ? current.Union(box) : box;
            }
        }

        return labels;
    }

    /// <summary>
    ///     Gets the box of the diagram title, or null when there is none.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The box.</returns>
    public static SceneRect? Title(Scene scene) =>
        scene.Walk().Select(entry => entry.Item).OfType<SceneText>().FirstOrDefault(text => text.Role == "title")?.Box;

    /// <summary>
    ///     Gets whether a box encloses another (within a thousandth of a pixel).
    /// </summary>
    /// <param name="outer">The outer box.</param>
    /// <param name="inner">The inner box.</param>
    /// <returns>Whether it does.</returns>
    public static bool Encloses(this SceneRect outer, SceneRect inner) =>
        inner.X >= outer.X - 0.001 && inner.Y >= outer.Y - 0.001 && inner.Right <= outer.Right + 0.001 && inner.Bottom <= outer.Bottom + 0.001;

    /// <summary>
    ///     Gets every item of a role.
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <param name="role">The role.</param>
    /// <returns>The items.</returns>
    public static List<SceneItem> OfRole(Scene scene, string role) =>
        [.. scene.Walk().Select(entry => entry.Item).Where(item => item.Role == role)];

    /// <summary>
    ///     Samples a path at evenly spaced lengths (the way SVG's <c>getPointAtLength</c> does): the curves are flattened
    ///     finely and the points are found along the flattened length.
    /// </summary>
    /// <param name="segments">The segments: moves, lines and curves (no arcs).</param>
    /// <param name="intervals">The number of intervals; the result has one point more.</param>
    /// <returns>The total length and the points at 0, 1/intervals, ..., 1 of it.</returns>
    public static (double Length, List<(double X, double Y)> Points) Sample(IReadOnlyList<SceneSegment> segments, int intervals)
    {
        const int steps = 256;
        var polyline = new List<(double X, double Y)>();
        var (x, y) = (0.0, 0.0);
        foreach (var s in segments)
        {
            switch (s.Kind)
            {
                case SceneSegmentKind.MoveTo:
                case SceneSegmentKind.LineTo:
                    polyline.Add((s.X, s.Y));
                    break;
                case SceneSegmentKind.QuadraticTo:
                    for (var i = 1; i <= steps; i++)
                    {
                        var t = (double)i / steps;
                        var u = 1 - t;
                        polyline.Add(((u * u * x) + (2 * u * t * s.X1) + (t * t * s.X), (u * u * y) + (2 * u * t * s.Y1) + (t * t * s.Y)));
                    }

                    break;
                case SceneSegmentKind.CubicTo:
                    for (var i = 1; i <= steps; i++)
                    {
                        var t = (double)i / steps;
                        var u = 1 - t;
                        polyline.Add((
                            (u * u * u * x) + (3 * u * u * t * s.X1) + (3 * u * t * t * s.X2) + (t * t * t * s.X),
                            (u * u * u * y) + (3 * u * u * t * s.Y1) + (3 * u * t * t * s.Y2) + (t * t * t * s.Y)));
                    }

                    break;
                default:
                    throw new NotSupportedException($"A {s.Kind} segment is not sampled.");
            }

            (x, y) = (s.X, s.Y);
        }

        if (polyline.Count < 2)
        {
            throw new ArgumentException("A path needs two points to be sampled.", nameof(segments));
        }
        var cumulative = new double[polyline.Count];
        for (var i = 1; i < polyline.Count; i++)
        {
            cumulative[i] = cumulative[i - 1] + Math.Sqrt(Math.Pow(polyline[i].X - polyline[i - 1].X, 2) + Math.Pow(polyline[i].Y - polyline[i - 1].Y, 2));
        }

        var length = cumulative[^1];

        var points = new List<(double X, double Y)>(intervals + 1);
        var j = 1;
        for (var k = 0; k <= intervals; k++)
        {
            var target = length * k / intervals;
            while (j < polyline.Count - 1 && cumulative[j] < target)
            {
                j++;
            }

            var span = cumulative[j] - cumulative[j - 1];
            var f = span > 0 ? Math.Clamp((target - cumulative[j - 1]) / span, 0, 1) : 1;
            points.Add((
                polyline[j - 1].X + (f * (polyline[j].X - polyline[j - 1].X)),
                polyline[j - 1].Y + (f * (polyline[j].Y - polyline[j - 1].Y))));
        }

        return (length, points);
    }

    /// <summary>
    ///     Reads the items of a sequence diagram's scene by role and id (its items are not translated).
    /// </summary>
    /// <param name="scene">The scene.</param>
    /// <returns>The items.</returns>
    public static SequenceScene SequenceItems(Scene scene) => new(scene);

    /// <summary>
    ///     Translates a segment.
    /// </summary>
    /// <param name="s">The segment.</param>
    /// <param name="dx">The x offset.</param>
    /// <param name="dy">The y offset.</param>
    /// <returns>The translated segment.</returns>
    private static SceneSegment Translate(SceneSegment s, double dx, double dy) => s.Kind switch
    {
        SceneSegmentKind.Close => s,
        SceneSegmentKind.ArcTo => s with { X = s.X + dx, Y = s.Y + dy },
        _ => s with { X = s.X + dx, Y = s.Y + dy, X1 = s.X1 + dx, Y1 = s.Y1 + dy, X2 = s.X2 + dx, Y2 = s.Y2 + dy },
    };

    /// <summary>
    ///     The items of a sequence diagram's scene: found by role and id, listed by role, and the paint order of the
    ///     top-level items as the reference goldens key it.
    /// </summary>
    internal sealed class SequenceScene
    {
        /// <summary>The roles of top-level items and the keys of the paint order.</summary>
        private static readonly Dictionary<string, string> OrderKeys = new(StringComparer.Ordinal)
        {
            ["actor-top"] = "actor-top",
            ["actor-bottom"] = "actor-bottom",
            ["lifeline"] = "lifeline",
            ["message"] = "message",
            ["sequence-number"] = "number",
            ["connection-circles"] = "circles",
            ["note"] = "note",
            ["frame"] = "frame",
            ["menu"] = "menu",
        };

        /// <summary>The items by role and id.</summary>
        private readonly Dictionary<(string Role, string Id), SceneItem> _byId = [];

        /// <summary>Every item, depth first in paint order.</summary>
        private readonly List<SceneItem> _all;

        /// <summary>
        ///     Initializes the items of a scene.
        /// </summary>
        /// <param name="scene">The scene.</param>
        public SequenceScene(Scene scene)
        {
            _all = [.. scene.Walk().Select(entry => entry.Item)];
            foreach (var item in _all.Where(item => item.Id is not null))
            {
                _byId.TryAdd((item.Role, item.Id!), item);
            }

            var activations = 0;
            var rects = 0;
            var boxes = 0;
            foreach (var item in scene.Items)
            {
                switch (item.Role)
                {
                    case "activation":
                        Order.Add(string.Create(CultureInfo.InvariantCulture, $"activation:{activations++}"));
                        break;
                    case "rect":
                        Order.Add(string.Create(CultureInfo.InvariantCulture, $"rect:{rects++}"));
                        break;
                    case "box":
                        Order.Add(string.Create(CultureInfo.InvariantCulture, $"box:{boxes++}"));
                        break;
                    case "title":
                        Order.Add("title");
                        break;
                    default:
                        if (OrderKeys.TryGetValue(item.Role, out var key))
                        {
                            Order.Add(key + ":" + item.Id);
                        }

                        break;
                }
            }
        }

        /// <summary>
        ///     Gets the paint order of the top-level items as the goldens key it (<c>actor-top:name</c>, <c>message:i0</c>,
        ///     <c>frame:i7</c>, <c>menu:actor0_popup</c>; activations, <c>rect</c> backgrounds and boxes by their index in
        ///     the order, as <c>activation:0</c>, <c>rect:0</c> and <c>box:0</c>).
        /// </summary>
        public List<string> Order { get; } = [];

        /// <summary>
        ///     Finds an item by role and id.
        /// </summary>
        /// <param name="role">The role.</param>
        /// <param name="id">The id.</param>
        /// <returns>The item, or null.</returns>
        public SceneItem? Find(string role, string id) => _byId.GetValueOrDefault((role, id));

        /// <summary>
        ///     Gets every item of a role, in paint order.
        /// </summary>
        /// <param name="role">The role.</param>
        /// <returns>The items.</returns>
        public IEnumerable<SceneItem> WithRole(string role) => _all.Where(item => item.Role == role);
    }
}
