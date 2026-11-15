using System.Numerics;
using DemaConsulting.CanvasNet.Geometry;
using DemaConsulting.CanvasNet.Positions;

namespace DemaConsulting.CanvasNet.Mermaid.Scenegraph;

/// <summary>
///     Reports the positions of the features of a laid-out scene (the <see cref="SceneItem.Position"/> tags the renderers
///     set) in the pixels of the surface a placement draws on.
/// </summary>
/// <remarks>
///     <para>
///         <b>Merge.</b> The items that carry tags with the same kind and id are one feature: a self-loop drawn as three
///         edge pieces, a gantt section drawn as one band per row, a commit bullet drawn as several shapes. The box is
///         the union of their geometry bounds (<see cref="Scene.BoundsOf(IReadOnlyList{SceneItem})"/>: curves exactly,
///         strokes not included).
///     </para>
///     <para>
///         <b>Aim.</b> The attachment pixel is the explicit aim point of the tag when it has one (a slice's mid-angle at
///         mid-radius), else the arc-length midpoint of the joined paths for a tag that aims along its path (edges and
///         messages; markers and labels are not part of the path), else the center of the box.
///     </para>
///     <para>
///         <b>Order.</b> Kinds come in <see cref="KindOrder"/> (structural features first), and the features of a kind
///         in source-line order (a feature without a line last), then drawing order. An edge or message gets its 0-based
///         index among the features of its kind with the same ends, in that order. A kind the filter rejects is skipped
///         before any geometry is computed.
///     </para>
///     <para>
///         The class is stateless and thread-safe.
///     </para>
/// </remarks>
internal static class ScenePositions
{
    /// <summary>
    ///     The kinds in report order; a kind not listed comes after them, in drawing order.
    /// </summary>
    internal static readonly string[] KindOrder =
    [
        "title", "group", "node", "participant", "task", "milestone", "section", "slice", "block", "commit", "branch",
        "tag", "edge", "message", "note"
    ];

    /// <summary>
    ///     The tolerance of curve flattening for the arc-length midpoint, in scene units.
    /// </summary>
    private const float FlattenTolerance = 0.01f;

    /// <summary>
    ///     Reports the positions of a scene to the handler of the options, when there is one.
    /// </summary>
    /// <param name="scene">The laid-out scene.</param>
    /// <param name="placement">Where the scene is drawn on the surface.</param>
    /// <param name="options">The options, whose <see cref="MermaidRenderOptions.PositionHandler"/> receives the positions.</param>
    public static void Report(Scene scene, ScenePlacement placement, MermaidRenderOptions options)
    {
        if (options.PositionHandler is not { } handler)
        {
            return;
        }

        foreach (var position in Collect(scene, placement, options.PositionKindFilter))
        {
            handler(position);
        }
    }

    /// <summary>
    ///     Builds the positions of a scene in report order.
    /// </summary>
    /// <param name="scene">The laid-out scene.</param>
    /// <param name="placement">Where the scene is drawn on the surface.</param>
    /// <param name="kindFilter">The kinds wanted, or null for every kind.</param>
    /// <returns>The positions.</returns>
    public static List<ImagePosition> Collect(Scene scene, ScenePlacement placement, Func<string, bool>? kindFilter)
    {
        // Group the tagged items by kind and id, keeping the first drawing order of each feature
        var features = new List<Feature>();
        var byKey = new Dictionary<(string Kind, string Id), Feature>();
        foreach (var (item, dx, dy) in scene.Walk())
        {
            if (item.Position is not { } tag || (kindFilter is not null && !kindFilter(tag.Kind)))
            {
                continue;
            }

            if (!byKey.TryGetValue((tag.Kind, tag.Id), out var feature))
            {
                feature = new Feature(tag);
                byKey[(tag.Kind, tag.Id)] = feature;
                features.Add(feature);
            }

            feature.Parts.Add((item, dx, dy));
        }

        // Order by kind priority, then source line (a feature without one last), then drawing order
        var ordered = features
            .Select((feature, order) => (feature, order))
            .OrderBy(entry => KindRank(entry.feature.Tag.Kind))
            .ThenBy(entry => entry.feature.Tag.Location.Line > 0 ? entry.feature.Tag.Location.Line : int.MaxValue)
            .ThenBy(entry => entry.order)
            .Select(entry => entry.feature);

        // Build each position, numbering edges and messages among those with the same ends
        var positions = new List<ImagePosition>();
        var parallel = new Dictionary<(string Kind, string? From, string? To), int>();
        foreach (var feature in ordered)
        {
            if (Build(feature, placement, parallel) is { } position)
            {
                positions.Add(position);
            }
        }

        return positions;
    }

    /// <summary>
    ///     Gets the arc-length midpoint of a sequence of paths, taken over their combined length.
    /// </summary>
    /// <param name="pieces">The paths in drawing order, each with the translation of its groups.</param>
    /// <returns>The midpoint, or null when the paths have no length.</returns>
    internal static (double X, double Y)? PathMidpoint(IReadOnlyList<(IReadOnlyList<SceneSegment> Segments, double Dx, double Dy)> pieces)
    {
        // Flatten every piece into straight lines in drawing order
        var lines = new List<(Vector2 From, Vector2 To)>();
        foreach (var (segments, dx, dy) in pieces)
        {
            Flatten(segments, dx, dy, lines);
        }

        var total = lines.Sum(line => (double)Vector2.Distance(line.From, line.To));
        if (lines.Count == 0 || !(total > 0))
        {
            return lines.Count == 0 ? null : (lines[0].From.X, lines[0].From.Y);
        }

        // Walk half the total length along the lines
        var remaining = total / 2;
        foreach (var (from, to) in lines)
        {
            var length = (double)Vector2.Distance(from, to);
            if (remaining <= length && length > 0)
            {
                var t = remaining / length;
                return (from.X + ((to.X - from.X) * t), from.Y + ((to.Y - from.Y) * t));
            }

            remaining -= length;
        }

        var last = lines[^1].To;
        return (last.X, last.Y);
    }

    /// <summary>
    ///     Gets the rank of a kind in the report order.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns>Its index in <see cref="KindOrder"/>, or the count of kinds for a kind not listed.</returns>
    private static int KindRank(string kind)
    {
        var rank = Array.IndexOf(KindOrder, kind);
        return rank < 0 ? KindOrder.Length : rank;
    }

    /// <summary>
    ///     Builds the position of one feature.
    /// </summary>
    /// <param name="feature">The feature and its items.</param>
    /// <param name="placement">Where the scene is drawn.</param>
    /// <param name="parallel">The count of edges and messages so far by kind and ends.</param>
    /// <returns>The position, or null when the feature draws nothing.</returns>
    private static ImagePosition? Build(Feature feature, ScenePlacement placement, Dictionary<(string Kind, string? From, string? To), int> parallel)
    {
        // The box: the union of the geometry bounds of every part
        SceneRect? bounds = null;
        foreach (var (item, dx, dy) in feature.Parts)
        {
            if (Scene.BoundsOf([item], dx, dy) is { } part)
            {
                bounds = bounds is { } current ? current.Union(part) : part;
            }
        }

        if (bounds is not { } box)
        {
            return null;
        }

        // The aim point, in scene coordinates
        var tag = feature.Tag;
        (double X, double Y)? aim = null;
        if (tag.Aim is { } explicitAim)
        {
            var (_, dx, dy) = feature.Parts[0];
            aim = (explicitAim.X + dx, explicitAim.Y + dy);
        }
        else if (tag.AlongPath)
        {
            aim = PathMidpoint([.. feature.Parts
                .Where(part => part.Item is ScenePath)
                .Select(part => (((ScenePath)part.Item).Segments, part.OffsetX, part.OffsetY))]);
        }

        var (aimX, aimY) = aim ?? (box.X + (box.Width / 2), box.Y + (box.Height / 2));

        // Map to the surface and fill in the facts of the tag
        var scale = placement.Scale;
        var pixelBox = new PositionBox((box.X * scale) + placement.OffsetX, (box.Y * scale) + placement.OffsetY, box.Width * scale, box.Height * scale);
        var pixel = new PositionPoint((aimX * scale) + placement.OffsetX, (aimY * scale) + placement.OffsetY);
        if (!pixelBox.IsValid() || !pixel.IsValid())
        {
            return null;
        }

        int? index = null;
        if (tag.From is not null || tag.To is not null)
        {
            var key = (tag.Kind, tag.From, tag.To);
            index = parallel.TryGetValue(key, out var count) ? count : 0;
            parallel[key] = index.Value + 1;
        }

        return new ImagePosition(tag.Kind, tag.Id)
        {
            Text = string.IsNullOrWhiteSpace(tag.Text) ? null : tag.Text.Trim(),
            Line = tag.Location.Line > 0 ? tag.Location.Line : null,
            From = tag.From,
            To = tag.To,
            Index = index,
            Values = tag.Value is { } value && double.IsFinite(value) ? [new("value", PositionValue.FromNumber(value))] : [],
            Box = pixelBox,
            Pixel = pixel
        };
    }

    /// <summary>
    ///     Flattens path segments into straight lines.
    /// </summary>
    /// <param name="segments">The segments.</param>
    /// <param name="dx">The horizontal translation.</param>
    /// <param name="dy">The vertical translation.</param>
    /// <param name="lines">The list the lines are added to.</param>
    private static void Flatten(IReadOnlyList<SceneSegment> segments, double dx, double dy, List<(Vector2 From, Vector2 To)> lines)
    {
        Vector2 Map(double x, double y) => new((float)(x + dx), (float)(y + dy));
        var current = Map(0, 0);
        var start = current;
        var points = new List<Vector2>();
        var curves = new List<(Vector2 Control1, Vector2 Control2, Vector2 End)>();
        foreach (var segment in segments)
        {
            // Each drawing command adds the lines from the current point through its flattened points
            points.Clear();
            switch (segment.Kind)
            {
                case SceneSegmentKind.MoveTo:
                    current = Map(segment.X, segment.Y);
                    start = current;
                    continue;
                case SceneSegmentKind.LineTo:
                    points.Add(Map(segment.X, segment.Y));
                    break;
                case SceneSegmentKind.QuadraticTo:
                    BezierFlattening.FlattenQuadratic(current, Map(segment.X1, segment.Y1), Map(segment.X, segment.Y), FlattenTolerance, points);
                    break;
                case SceneSegmentKind.CubicTo:
                    BezierFlattening.FlattenCubic(current, Map(segment.X1, segment.Y1), Map(segment.X2, segment.Y2), Map(segment.X, segment.Y), FlattenTolerance, points);
                    break;
                case SceneSegmentKind.ArcTo:
                    curves.Clear();
                    SvgArcConverter.ToBeziers(
                        current,
                        new Vector2((float)Math.Abs(segment.X1), (float)Math.Abs(segment.Y1)),
                        (float)segment.X2,
                        segment.LargeArc,
                        segment.Sweep,
                        Map(segment.X, segment.Y),
                        curves);
                    var from = current;
                    foreach (var (control1, control2, end) in curves)
                    {
                        BezierFlattening.FlattenCubic(from, control1, control2, end, FlattenTolerance, points);
                        from = end;
                    }

                    if (curves.Count == 0)
                    {
                        points.Add(Map(segment.X, segment.Y));
                    }

                    break;
                default:
                    points.Add(start);
                    break;
            }

            foreach (var point in points)
            {
                lines.Add((current, point));
                current = point;
            }
        }
    }

    /// <summary>
    ///     One feature: its tag and the items that draw it, with the translation of their groups.
    /// </summary>
    /// <param name="Tag">The tag of the first item.</param>
    private sealed record Feature(ScenePositionTag Tag)
    {
        /// <summary>
        ///     Gets the items that draw the feature, in drawing order.
        /// </summary>
        public List<(SceneItem Item, double OffsetX, double OffsetY)> Parts { get; } = [];
    }
}
