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
    ///     Gets the edge label centers by edge id.
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
                labels[id] = (dx + group.OffsetX, dy + group.OffsetY);
            }
        }

        return labels;
    }

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
}
