using System.Globalization;
using System.Numerics;
using DemaConsulting.CanvasNet.Positions;
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Guides;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Scenegraph;

/// <summary>
///     Reports the positions of the features of a laid-out chart (titles, views, axes, legends and marks) in the pixels
///     of the surface a placement draws on, from the tags the chart builders set on the scene.
/// </summary>
/// <remarks>
///     <para>
///         <b>Views.</b> Every view has a canonical path built from the view tree (<see cref="ViewPath(LayerNode)"/>):
///         <c>""</c> for the root, then <c>hconcat[1]</c>, <c>vconcat[0].layer[1]</c>, <c>facet[3]</c>,
///         <c>repeat[2]</c>, one segment per level. The <c>name</c> the specification gives a view is reported separately.
///     </para>
///     <para>
///         <b>Kinds and ids.</b> A <c>title</c> is <c>title</c>; a <c>view</c> (the data rectangle of a chart) is its
///         path; an <c>axis</c> is its channel; a <c>legend</c> is its first channel; a <c>mark</c> is
///         <c>&lt;mark&gt;-&lt;n&gt;</c>, <c>n</c> counting the items of that mark type in that view from 0 in drawing order
///         (each vertex of a line, area or trail counts).
///     </para>
///     <para>
///         <b>Geometry.</b> The box of a guide is its bounds (a view's data rectangle, a legend's frame); a mark's box is
///         its geometry bounds without the stroke: a bar's rectangle, a symbol's shape, a text's measured box, an arc's
///         sector, a rule's segment, and a zero-size box at a vertex. The pixel is the box center, except: a rule's
///         midpoint, a text's anchor, an arc's mid-angle at mid-radius, and a vertex.
///     </para>
///     <para>
///         <b>Order.</b> Titles, views, axes and legends, then marks, each in drawing order. A kind the filter rejects
///         is skipped before its geometry is computed. The class is stateless and thread-safe.
///     </para>
/// </remarks>
internal static class ScenePositions
{
    /// <summary>
    ///     The guide kinds in report order; marks come after them.
    /// </summary>
    private static readonly string[] GuideOrder = ["title", "view", "axis", "legend"];

    /// <summary>
    ///     Reports the positions of a scene to the handler of the options, when there is one.
    /// </summary>
    /// <param name="scene">The laid-out scene.</param>
    /// <param name="placement">Where the scene is drawn on the surface.</param>
    /// <param name="measurer">The text measurer of the render (the box of a text mark).</param>
    /// <param name="options">The options, whose <see cref="VegaLiteRenderOptions.PositionHandler"/> receives the positions.</param>
    public static void Report(Scene scene, ScenePlacement placement, TextMeasurer measurer, VegaLiteRenderOptions options)
    {
        if (options.PositionHandler is not { } handler)
        {
            return;
        }

        foreach (var position in Collect(scene, placement, measurer, options.PositionKindFilter))
        {
            handler(position);
        }
    }

    /// <summary>
    ///     Builds the positions of a scene in report order.
    /// </summary>
    /// <param name="scene">The laid-out scene.</param>
    /// <param name="placement">Where the scene is drawn on the surface.</param>
    /// <param name="measurer">The text measurer of the render.</param>
    /// <param name="kindFilter">The kinds wanted, or null for every kind.</param>
    /// <returns>The positions.</returns>
    public static List<ImagePosition> Collect(Scene scene, ScenePlacement placement, TextMeasurer measurer, Func<string, bool>? kindFilter)
    {
        bool Wanted(string kind) => kindFilter is null || kindFilter(kind);
        var guides = GuideOrder.ToDictionary(kind => kind, _ => new List<ImagePosition>(), StringComparer.Ordinal);
        var marks = new List<ImagePosition>();
        var wantMarks = Wanted("mark");
        var counters = new Dictionary<(string View, string Mark), int>();

        // Walk the scene in drawing order with the translation of the groups and the context of the marks group
        var pending = new Stack<(SceneItem Item, double X, double Y, SceneMarkContext? Context)>();
        pending.Push((scene.Root, 0, 0, null));
        while (pending.Count > 0)
        {
            var (item, x, y, context) = pending.Pop();
            if (item.Position is { } tag && guides.TryGetValue(tag.Kind, out var list) && Wanted(tag.Kind))
            {
                if (Guide(tag, item, x, y, placement) is { } guide)
                {
                    list.Add(guide);
                }
            }
            else if (wantMarks && context is not null)
            {
                AddMarks(item, x, y, context, measurer, placement, counters, marks);
            }

            if (item is SceneGroup group)
            {
                var inner = group.Marks ?? context;
                for (var i = group.Children.Count - 1; i >= 0; i--)
                {
                    pending.Push((group.Children[i], x + group.X, y + group.Y, inner));
                }
            }
        }

        return [.. GuideOrder.SelectMany(kind => guides[kind]), .. marks];
    }

    /// <summary>
    ///     Gets the canonical path of a view: one <c>operator[index]</c> segment per level below the root, joined by
    ///     <c>.</c>; <c>""</c> for the root.
    /// </summary>
    /// <remarks>
    ///     The operator is that of the parent: <c>concat</c>, <c>hconcat</c>, <c>vconcat</c>, <c>repeat</c> or
    ///     <c>facet</c> (the index of a facet's cell is its place in the layout), and <c>layer</c> for a layered view. The
    ///     views a composite mark (a boxplot) expands to add no segment: they are the composite mark's view.
    /// </remarks>
    /// <param name="node">The view.</param>
    /// <returns>The path.</returns>
    internal static string ViewPath(LayerNode node)
    {
        var segments = new List<string>();
        for (var current = node; current.Parent is { } parent; current = parent)
        {
            // The layers a composite mark expands to are written by the expansion: they are the composite mark's view
            if (parent.Kind == ViewKind.Expansion || current.IsSynthetic)
            {
                continue;
            }

            var index = IndexOf(parent.Children, current);
            var name = parent.Kind == ViewKind.Layer ? "layer" : parent.Operator ?? "layer";
            segments.Add(string.Create(CultureInfo.InvariantCulture, $"{name}[{index}]"));
        }

        segments.Reverse();
        return string.Join('.', segments);
    }

    /// <summary>
    ///     Gets the <c>name</c> the specification gives a view.
    /// </summary>
    /// <param name="node">The view.</param>
    /// <returns>The name, or null.</returns>
    internal static string? ViewName(LayerNode node) => node.Source["name"]?.StringValue;

    /// <summary>
    ///     Gets the values of the facet channels of the cell a view is in.
    /// </summary>
    /// <param name="node">The view.</param>
    /// <returns><c>row</c> and <c>column</c> for the fields a facet has, or <c>facet</c> for a wrapped one; empty outside a facet.</returns>
    internal static IReadOnlyList<KeyValuePair<string, PositionValue>> FacetValues(LayerNode node)
    {
        // The nearest view that is a cell of a facet knows the values; the facet above it knows which fields it has
        for (var current = node; current is not null; current = current.Parent)
        {
            if (current.Cell is { } cell && current.Parent is { Facet: { } facet })
            {
                var values = new List<KeyValuePair<string, PositionValue>>();
                Add(values, "column", facet.Column is not null, cell.ColumnValue);
                Add(values, "facet", facet.Wrap is not null, cell.WrapValue);
                Add(values, "row", facet.Row is not null, cell.RowValue);
                return values;
            }
        }

        return [];

        static void Add(List<KeyValuePair<string, PositionValue>> values, string name, bool present, DataValue value)
        {
            if (present && Marks.MarkBuilder.TryConvert(value, out var converted))
            {
                values.Add(new KeyValuePair<string, PositionValue>(name, converted));
            }
        }
    }

    /// <summary>
    ///     Finds a view among the children of its parent by reference.
    /// </summary>
    /// <param name="children">The children.</param>
    /// <param name="child">The view.</param>
    /// <returns>The index.</returns>
    private static int IndexOf(IReadOnlyList<LayerNode> children, LayerNode child)
    {
        for (var index = 0; index < children.Count; index++)
        {
            if (ReferenceEquals(children[index], child))
            {
                return index;
            }
        }

        return 0;
    }

    /// <summary>
    ///     Builds the position of a guide or a view.
    /// </summary>
    /// <param name="tag">The tag of the group.</param>
    /// <param name="item">The tagged item.</param>
    /// <param name="x">The x translation of the item's parent.</param>
    /// <param name="y">The y translation of the item's parent.</param>
    /// <param name="placement">Where the scene is drawn.</param>
    /// <returns>The position, or null when its geometry is not finite.</returns>
    private static ImagePosition? Guide(ScenePositionTag tag, SceneItem item, double x, double y, ScenePlacement placement)
    {
        // The tag's own bounds, else the group's rectangle (a view's data rectangle, a legend's frame)
        var bounds = new SceneBounds(x, y, x, y);
        if (tag.Bounds is { IsEmpty: false } own)
        {
            bounds = own.Translate(x, y);
        }
        else if (item is SceneGroup group)
        {
            bounds = new SceneBounds(x + group.X, y + group.Y, x + group.X + group.Width, y + group.Y + group.Height);
        }
        var box = Map(bounds, placement);
        if (!box.IsValid())
        {
            return null;
        }

        return new ImagePosition(tag.Kind, tag.Id)
        {
            Text = string.IsNullOrWhiteSpace(tag.Text) ? null : tag.Text,
            View = tag.View,
            ViewName = tag.ViewName,
            Box = box,
            Pixel = box.Center
        };
    }

    /// <summary>
    ///     Adds the positions of a mark item: one for a bar, rule, symbol, text, arc or image, one per vertex of a path.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="x">The x translation of the marks group.</param>
    /// <param name="y">The y translation of the marks group.</param>
    /// <param name="context">The context of the marks group.</param>
    /// <param name="measurer">The text measurer.</param>
    /// <param name="placement">Where the scene is drawn.</param>
    /// <param name="counters">The number of items of each mark type in each view so far.</param>
    /// <param name="marks">The list the positions are added to.</param>
    private static void AddMarks(
        SceneItem item,
        double x,
        double y,
        SceneMarkContext context,
        TextMeasurer measurer,
        ScenePlacement placement,
        Dictionary<(string View, string Mark), int> counters,
        List<ImagePosition> marks)
    {
        var mark = item.Role.StartsWith("mark-", StringComparison.Ordinal) ? item.Role[5..] : item.Role;
        IReadOnlyList<SceneVertex>? vertices = item switch
        {
            ScenePath path => path.Vertices,
            SceneTrail trail => trail.Vertices,
            _ => null,
        };

        // A path mark is one position per vertex, at the vertex
        if (vertices is not null)
        {
            foreach (var vertex in vertices)
            {
                var point = Map(new SceneBounds(x + vertex.X, y + vertex.Y, x + vertex.X, y + vertex.Y), placement);
                if (point.IsValid())
                {
                    marks.Add(Mark(context, mark, vertex.Row, point, null, counters));
                }
            }

            return;
        }

        if (item.Datum < 0 || Geometry(item, measurer) is not { } geometry)
        {
            return;
        }

        var box = Map(geometry.Bounds.Translate(x, y), placement);
        var pixel = geometry.Aim is { } aim ? MapPoint(aim.X + x, aim.Y + y, placement) : box.Center;
        if (box.IsValid() && pixel.IsValid())
        {
            marks.Add(Mark(context, mark, item.Datum, box, pixel, counters));
        }
    }

    /// <summary>
    ///     Builds the position of one mark item or vertex.
    /// </summary>
    /// <param name="context">The context of the marks group.</param>
    /// <param name="mark">The mark type.</param>
    /// <param name="row">The row it draws.</param>
    /// <param name="box">The box in surface pixels.</param>
    /// <param name="pixel">The attachment pixel, or null for the box center.</param>
    /// <param name="counters">The number of items of each mark type in each view so far.</param>
    /// <returns>The position.</returns>
    private static ImagePosition Mark(
        SceneMarkContext context,
        string mark,
        int row,
        PositionBox box,
        PositionPoint? pixel,
        Dictionary<(string View, string Mark), int> counters)
    {
        var key = (context.View, mark);
        var number = counters.GetValueOrDefault(key);
        counters[key] = number + 1;
        return new ImagePosition("mark", string.Create(CultureInfo.InvariantCulture, $"{mark}-{number}"))
        {
            View = context.View,
            ViewName = context.ViewName,
            Mark = mark,
            Values = [.. context.Values(row), .. context.FacetValues],
            Box = box,
            Pixel = pixel ?? box.Center
        };
    }

    /// <summary>
    ///     Gets the geometry bounds and the aim point of a mark item in the coordinates of its marks group.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="measurer">The text measurer.</param>
    /// <returns>The bounds and the aim point (null for the center of the bounds), or null for an item without geometry.</returns>
    internal static (SceneBounds Bounds, (double X, double Y)? Aim)? Geometry(SceneItem item, TextMeasurer measurer)
    {
        switch (item)
        {
            case SceneRect rect:
                return (Normalized(rect.X, rect.Y, rect.X + rect.Width, rect.Y + rect.Height), null);
            case SceneRule rule:
                return (Normalized(rule.X, rule.Y, rule.X2, rule.Y2), ((rule.X + rule.X2) / 2, (rule.Y + rule.Y2) / 2));
            case SceneSymbol symbol:
                var shape = ScenePainter.SymbolPath(symbol.Shape, symbol.Size)
                    .Transform(Matrix3x2.CreateRotation((float)(symbol.Angle * Math.PI / 180)) * Matrix3x2.CreateTranslation((float)symbol.X, (float)symbol.Y));
                var symbolBounds = shape.GetBounds(0.01f);
                return (symbolBounds.IsEmpty ? new SceneBounds(symbol.X, symbol.Y, symbol.X, symbol.Y) : new SceneBounds(symbolBounds.X, symbolBounds.Y, symbolBounds.X + symbolBounds.Width, symbolBounds.Y + symbolBounds.Height), (symbol.X, symbol.Y));
            case SceneText text:
                return (measurer.GetBounds(text), (text.X + text.Dx, text.Y + text.Dy));
            case SceneArc arc:
                var sector = ArcGeometry.Build(arc.InnerRadius, arc.OuterRadius, arc.StartAngle, arc.EndAngle, arc.PadAngle, arc.CornerRadius).GetBounds(0.01f);
                var middle = ((arc.StartAngle + arc.EndAngle) / 2) - (Math.PI / 2);
                var radius = (arc.InnerRadius + arc.OuterRadius) / 2;
                var arcBounds = sector.IsEmpty
                    ? new SceneBounds(arc.X, arc.Y, arc.X, arc.Y)
                    : new SceneBounds(arc.X + sector.X, arc.Y + sector.Y, arc.X + sector.X + sector.Width, arc.Y + sector.Y + sector.Height);
                return (arcBounds, (arc.X + (radius * Math.Cos(middle)), arc.Y + (radius * Math.Sin(middle))));
            case SceneImage image:
                var width = image.Width ?? 0;
                var height = image.Height ?? 0;
                var left = image.Align switch { "center" => image.X - (width / 2), "right" => image.X - width, _ => image.X };
                var top = image.Baseline switch { "middle" => image.Y - (height / 2), "bottom" => image.Y - height, _ => image.Y };
                return (new SceneBounds(left, top, left + width, top + height), null);
            default:
                return null;
        }
    }

    /// <summary>
    ///     Makes bounds from two corners in any order.
    /// </summary>
    /// <param name="x1">The x of one corner.</param>
    /// <param name="y1">The y of one corner.</param>
    /// <param name="x2">The x of the other corner.</param>
    /// <param name="y2">The y of the other corner.</param>
    /// <returns>The bounds.</returns>
    private static SceneBounds Normalized(double x1, double y1, double x2, double y2) =>
        new(Math.Min(x1, x2), Math.Min(y1, y2), Math.Max(x1, x2), Math.Max(y1, y2));

    /// <summary>
    ///     Maps scene bounds to a box in surface pixels.
    /// </summary>
    /// <param name="bounds">The bounds in scene coordinates.</param>
    /// <param name="placement">Where the scene is drawn.</param>
    /// <returns>The box.</returns>
    private static PositionBox Map(SceneBounds bounds, ScenePlacement placement) => new(
        (bounds.X1 * placement.Scale) + placement.OffsetX,
        (bounds.Y1 * placement.Scale) + placement.OffsetY,
        Math.Max(0, bounds.X2 - bounds.X1) * placement.Scale,
        Math.Max(0, bounds.Y2 - bounds.Y1) * placement.Scale);

    /// <summary>
    ///     Maps a scene point to surface pixels.
    /// </summary>
    /// <param name="x">The x in scene coordinates.</param>
    /// <param name="y">The y in scene coordinates.</param>
    /// <param name="placement">Where the scene is drawn.</param>
    /// <returns>The point.</returns>
    private static PositionPoint MapPoint(double x, double y, ScenePlacement placement) =>
        new((x * placement.Scale) + placement.OffsetX, (y * placement.Scale) + placement.OffsetY);
}
