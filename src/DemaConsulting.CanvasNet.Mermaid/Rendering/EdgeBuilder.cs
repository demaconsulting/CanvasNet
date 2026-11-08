// Portions derived from mermaid (MIT); see third-party/README.md
using System.Text;
using System.Text.RegularExpressions;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Dagre;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;

namespace DemaConsulting.CanvasNet.Mermaid.Rendering;

/// <summary>
///     One end of an edge as <c>insertEdge</c> sees it: the node's box after the layout and its intersect function (a
///     node without one, such as a laid-out subgraph, keeps the layout's end points).
/// </summary>
/// <param name="Box">The node's box.</param>
/// <param name="Intersect">The intersect function, or null.</param>
internal sealed record EdgeEnd(NodeBox Box, ShapeIntersect? Intersect);

/// <summary>
///     An edge to draw: its layout data and the route the layout gave it.
/// </summary>
/// <param name="Data">The edge of the layout data.</param>
/// <param name="Id">The id of the drawn edge.</param>
/// <param name="Points">The route of the layout (dagre's points, ending on the node borders).</param>
internal sealed record EdgeRoute(LayoutEdge Data, string Id, IReadOnlyList<ShapePoint> Points)
{
    /// <summary>Gets the arrow type at the start.</summary>
    public string ArrowTypeStart { get; init; } = Data.ArrowTypeStart;

    /// <summary>Gets the arrow type at the end.</summary>
    public string ArrowTypeEnd { get; init; } = Data.ArrowTypeEnd;

    /// <summary>Gets the box of the subgraph the edge leaves (the route is cut at its border), or null.</summary>
    public NodeBox? FromCluster { get; init; }

    /// <summary>Gets the box of the subgraph the edge enters (the route is cut at its border), or null.</summary>
    public NodeBox? ToCluster { get; init; }
}

/// <summary>
///     A drawn edge: its path and markers, the box of its path and the routes its label is placed on.
/// </summary>
/// <param name="Items">The path and the markers, in drawing order.</param>
/// <param name="Bounds">The box of the path geometry (SVG's <c>getBBox</c>, markers and stroke not included), or null.</param>
/// <param name="UpdatedPath">The cut route when it changed (Mermaid's <c>paths.updatedPath</c>), or null.</param>
/// <param name="OriginalPath">The layout's route (<c>paths.originalPath</c>).</param>
/// <param name="PathData">The SVG path data Mermaid writes, or null for no path.</param>
internal sealed record EdgeDrawing(
    IReadOnlyList<SceneItem> Items,
    SceneRect? Bounds,
    IReadOnlyList<ShapePoint>? UpdatedPath,
    IReadOnlyList<ShapePoint> OriginalPath,
    string? PathData);

/// <summary>
///     A placed edge label: its items and its box.
/// </summary>
/// <param name="Items">The items (backgrounds and text) around the label's position.</param>
/// <param name="Bounds">The box of the label, or null when it is empty.</param>
internal sealed record EdgeLabelDrawing(IReadOnlyList<SceneItem> Items, SceneRect? Bounds);

/// <summary>
///     Mermaid's edges (<c>rendering-elements/edges.js</c>, <c>edgeMarker.ts</c>, <c>markers.js</c> and
///     <c>utils/lineWithOffset.ts</c>): the route from the layout cut at the node outlines and subgraph borders, drawn
///     with a d3 curve, stroked and marked, and its label measured, backed and placed.
/// </summary>
/// <remarks>
///     <para>
///         <b>Route.</b> The layout's first and last points are replaced by the intersections of the node outlines with
///         the lines to the inner points (<c>insertEdge</c>); an edge into or out of a subgraph is cut at the subgraph's
///         border (<c>cutPathAtIntersect</c>). Right-angle corners are rounded off (<c>fixCorners</c>) except for the
///         <c>rounded</c> curve, which rounds every corner itself, and the ends are pulled back by the arrowhead's offset
///         so the arrowhead's tip touches the node.
///     </para>
///     <para>
///         <b>Path.</b> The points are drawn with d3's line generator and the edge's curve (<c>basis</c> by default), and
///         the numbers are rounded to 3 decimals as d3 writes them. The stroke follows the style sheet's classes
///         (<c>edge-thickness-*</c>, <c>edge-pattern-*</c>, <c>flowchart-link</c>, the animation classes, drawn as their
///         first frame) and the edge's own styles (<c>linkStyle</c>, <c>classDef</c>).
///     </para>
///     <para>
///         <b>Markers.</b> The <c>point</c>, <c>circle</c> and <c>cross</c> markers are drawn in the style sheet's
///         <c>.marker</c> color, or in the edge's stroke color when its styles give one, as Mermaid's colored marker
///         copies are.
///     </para>
///     <para>
///         <b>Labels.</b> The label is measured as <c>insertEdgeLabel</c> creates it (a 200 pixel wrapping width, with
///         a background) and placed at the layout's label position, or on the middle of the cut route when the route
///         changed (<c>positionEdgeLabel</c>).
///     </para>
///     <para>
///         The class is stateless and safe for concurrent use.
///     </para>
/// </remarks>
internal static partial class EdgeBuilder
{
    /// <summary>The wrapping width of an edge label (<c>createText</c>'s default, which <c>insertEdgeLabel</c> keeps).</summary>
    internal const double LabelWidth = 200;

    /// <summary>The offsets that pull the line's ends back for the arrowheads (<c>markerOffsets</c>).</summary>
    private static readonly Dictionary<string, double> MarkerOffsets = new(StringComparer.Ordinal)
    {
        ["aggregation"] = 17.25,
        ["extension"] = 17.25,
        ["composition"] = 17.25,
        ["dependency"] = 6,
        ["lollipop"] = 13.5,
        ["arrow_point"] = 4,
        ["arrow_barb"] = 0,
        ["arrow_barb_neo"] = 5.5,
    };

    /// <summary>The first stroke declaration of a style text (<c>/stroke:([^;]+)/</c>).</summary>
    private static readonly Regex StrokePattern = new("stroke:([^;]+)", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>
    ///     Measures an edge's label as <c>insertEdgeLabel</c> creates it.
    /// </summary>
    /// <param name="edge">The edge.</param>
    /// <param name="label">The label text.</param>
    /// <param name="context">The settings of the rendering.</param>
    /// <returns>The measured label (an empty box for an empty label).</returns>
    public static LabelLayout MeasureLabel(LayoutEdge edge, string label, ShapeContext context)
    {
        ArgumentNullException.ThrowIfNull(edge);
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(context);
        var html = context.Config.HtmlLabels;
        if (label.Length == 0)
        {
            return LabelLayout.Of(html, new SceneRect(0, 0, 0, 0));
        }

        var (labelItems, _) = ShapeLibrary.SplitStyles(edge.CssCompiledStyles.Concat(edge.LabelStyle));
        var labelStyles = StyleDeclarations.ParseItems(labelItems, edge.Location, context.Sink);
        var css = context.Styles.NodeLabel;
        return context.MeasureLabel(new LabelRequest(label, ShapeLibrary.LabelFontOf(labelStyles, css))
        {
            Markdown = edge.LabelType == "markdown",
            HtmlLabels = html,
            Width = LabelWidth,
            IsNode = false,
            AddBackground = true,
            Prepare = false,
            MarkdownAutoWrap = context.Config.MarkdownAutoWrap,
            RootFontSize = css.FontSize,
            ConfigFontSize = context.Config.FontSize,
            CenterSvgText = true,
            Location = edge.Location,
        });
    }

    /// <summary>
    ///     Draws an edge (Mermaid's <c>insertEdge</c>).
    /// </summary>
    /// <param name="edge">The edge and its route.</param>
    /// <param name="tail">The start node.</param>
    /// <param name="head">The end node.</param>
    /// <param name="context">The settings of the rendering.</param>
    /// <returns>The drawn edge.</returns>
    public static EdgeDrawing Draw(EdgeRoute edge, EdgeEnd? tail, EdgeEnd? head, ShapeContext context)
    {
        ArgumentNullException.ThrowIfNull(edge);
        ArgumentNullException.ThrowIfNull(context);
        var data = edge.Data;
        var original = edge.Points;
        var points = original.ToList();
        var pointsHaveChanged = false;

        // The ends become the intersections with the node outlines
        if (head?.Intersect is { } headIntersect && tail?.Intersect is { } tailIntersect && points.Count > 2)
        {
            points = points.GetRange(1, points.Count - 2);
            points.Insert(0, tailIntersect(tail.Box, points[0]));
            points.Add(headIntersect(head.Box, points[^1]));
        }

        if (edge.ToCluster is { } toCluster)
        {
            points = CutPathAtIntersect(original, toCluster);
            pointsHaveChanged = true;
        }

        if (edge.FromCluster is { } fromCluster)
        {
            points.Reverse();
            points = CutPathAtIntersect(points, fromCluster);
            points.Reverse();
            pointsHaveChanged = true;
        }

        var lineData = points.Where(p => !double.IsNaN(p.Y)).ToList();
        var curve = data.Curve ?? context.Config.Flowchart.Curve;
        if (curve != "rounded")
        {
            lineData = FixCorners(lineData);
        }

        var (segments, pathData) = curve == "rounded"
            ? RoundedPath(ApplyMarkerOffsetsToPoints(lineData, edge.ArrowTypeStart, edge.ArrowTypeEnd), 5)
            : CurveWithOffsets(lineData, edge.ArrowTypeStart, edge.ArrowTypeEnd, curve);

        // The path, styled by its classes and its own styles, then its markers
        var items = new List<SceneItem>();
        var (style, strokeText) = PathStyle(edge, context);
        SceneRect? bounds = null;
        if (pathData is not null && segments.Count > 0)
        {
            var path = PathOf(segments, style, edge.Id, data.Location);
            items.Add(path);
            bounds = Scene.BoundsOf([path]);
            items.AddRange(DrawMarkers(segments, edge.ArrowTypeStart, edge.ArrowTypeEnd, shape => MarkerPaintOf(shape, strokeText, context.Styles), edge.Id)
                .Select(item => item with { Location = data.Location }));
        }

        // The label moves to the cut route when the route's middle point is not on the drawn path
        if (points.Count > 0 && !IsLabelCoordinateInPath(points[points.Count / 2], pathData ?? string.Empty))
        {
            pointsHaveChanged = true;
        }

        return new EdgeDrawing(items, bounds, pointsHaveChanged ? points : null, original, pathData);
    }

    /// <summary>
    ///     Gets where an edge's label goes (<c>positionEdgeLabel</c>): the layout's label position, or the middle of the
    ///     cut route when the route changed, moved down by half the subgraph title margins.
    /// </summary>
    /// <param name="label">The label text.</param>
    /// <param name="x">The layout's label x, or null when the layout placed none.</param>
    /// <param name="y">The layout's label y, or null.</param>
    /// <param name="drawing">The drawn edge.</param>
    /// <param name="titleMargin">The total subgraph title margin.</param>
    /// <returns>The position of the label's center, or null when the edge has no label.</returns>
    public static ShapePoint? LabelPosition(string label, double? x, double? y, EdgeDrawing drawing, double titleMargin)
    {
        ArgumentNullException.ThrowIfNull(drawing);
        if (string.IsNullOrEmpty(label))
        {
            return null;
        }

        if (drawing.UpdatedPath is { } updated && CalcLabelPosition(updated) is { } position)
        {
            return position with { Y = position.Y + (titleMargin / 2) };
        }

        // A label without a layout position gets an invalid transform, which the browser ignores
        return x is { } lx && y is { } ly && double.IsFinite(lx) && double.IsFinite(ly)
            ? new ShapePoint(lx, ly + (titleMargin / 2))
            : new ShapePoint(0, 0);
    }

    /// <summary>
    ///     Draws a placed edge label: its backgrounds and its text, centered on its position (<c>insertEdgeLabel</c>'s
    ///     centering transform).
    /// </summary>
    /// <param name="label">The measured label.</param>
    /// <param name="position">The position of the label's center.</param>
    /// <param name="edge">The edge.</param>
    /// <param name="id">The id of the drawn edge.</param>
    /// <param name="styles">The style sheet.</param>
    /// <returns>The items and the box.</returns>
    public static EdgeLabelDrawing DrawLabel(LabelLayout label, ShapePoint position, LayoutEdge edge, string id, FlowchartStyles styles)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(edge);
        ArgumentNullException.ThrowIfNull(styles);
        if (!(label.Width > 0 && label.Height > 0))
        {
            return new EdgeLabelDrawing([], null);
        }

        // HTML labels are centered by their box; SVG labels by the box of their text (the background not included)
        var textBox = label.IsHtml
            ? label.Box
            : new SceneRect(
                label.Box.X + LabelBuilder.SvgBackgroundPadding,
                label.Box.Y + LabelBuilder.SvgBackgroundPadding,
                label.Box.Width - (2 * LabelBuilder.SvgBackgroundPadding),
                label.Box.Height - (2 * LabelBuilder.SvgBackgroundPadding));
        var dx = label.IsHtml ? -label.Width / 2 : -(textBox.X + (textBox.Width / 2));
        var dy = label.IsHtml ? -label.Height / 2 : -(textBox.Y + (textBox.Height / 2));
        var box = label.Box.Offset(dx, dy);
        var children = new List<SceneItem>();
        void Background(Rgba32? color, double opacity, string role)
        {
            if (color is { } c)
            {
                var fill = opacity < 1 ? c with { A = (byte)Math.Round(c.A * opacity, MidpointRounding.AwayFromZero) } : c;
                children.Add(new ScenePath(RectSegments(box)) { Role = role, Id = id, Location = edge.Location, Fill = fill });
            }
        }

        if (label.IsHtml)
        {
            // The div's labelBkg background, then the paragraph's .edgeLabel p background, both over the whole box
            Background(styles.Resolve(".labelBkg").Paint("background-color"), 1, "edge-label-background");
            Background(styles.Resolve(".edgeLabel p").Paint("background-color"), 1, "edge-label-background");
        }
        else
        {
            // The background rectangle of the SVG text (.edgeLabel rect)
            var rect = styles.Resolve(".edgeLabel rect");
            var opacity = rect["opacity"] is { } text && StyleDeclarations.TryParseOpacity(text, out var factor) ? factor : 1;
            Background(rect.Paint("fill"), opacity, "edge-label-background");
        }

        children.AddRange(label.ToScene(dx, dy, "label", id, edge.Location));
        var group = new SceneGroup(position.X, position.Y, children) { Role = "edge-label", Id = id, Location = edge.Location };
        return new EdgeLabelDrawing([group], box.Offset(position.X, position.Y));
    }

    /// <summary>
    ///     Gets the middle of a route by length (Mermaid's <c>calcLabelPosition</c>), or null for an empty route.
    /// </summary>
    /// <param name="points">The route.</param>
    /// <returns>The point, or null.</returns>
    internal static ShapePoint? CalcLabelPosition(IReadOnlyList<ShapePoint> points)
    {
        if (points.Count == 0)
        {
            return null;
        }

        if (points.Count == 1)
        {
            return points[0];
        }

        var total = 0.0;
        for (var i = 1; i < points.Count; i++)
        {
            total += Distance(points[i], points[i - 1]);
        }

        // calculatePoint: walk half the length
        var remaining = total / 2;
        for (var i = 1; i < points.Count; i++)
        {
            var (previous, point) = (points[i - 1], points[i]);
            var vector = Distance(point, previous);
            if (vector == 0)
            {
                return previous;
            }

            if (vector < remaining)
            {
                remaining -= vector;
                continue;
            }

            var ratio = remaining / vector;
            if (ratio <= 0)
            {
                return previous;
            }

            if (ratio >= 1)
            {
                return point;
            }

            return new ShapePoint(
                RoundNumber(((1 - ratio) * previous.X) + (ratio * point.X), 5),
                RoundNumber(((1 - ratio) * previous.Y) + (ratio * point.Y), 5));
        }

        return null;
    }

    /// <summary>
    ///     Gets whether a point's rounded coordinates appear in path data whose decimals are rounded (Mermaid's
    ///     <c>isLabelCoordinateInPath</c>, a text search).
    /// </summary>
    /// <param name="point">The point.</param>
    /// <param name="pathData">The path data.</param>
    /// <returns>Whether either rounded coordinate appears in the text.</returns>
    internal static bool IsLabelCoordinateInPath(ShapePoint point, string pathData)
    {
        var roundedX = Js(JsRound(point.X));
        var roundedY = Js(JsRound(point.Y));

        // d.replace(/(\d+\.\d+)/g, match => Math.round(parseFloat(match)).toString())
        var sanitized = new StringBuilder(pathData.Length);
        var i = 0;
        while (i < pathData.Length)
        {
            if (!char.IsAsciiDigit(pathData[i]))
            {
                sanitized.Append(pathData[i++]);
                continue;
            }

            var j = i;
            while (j < pathData.Length && char.IsAsciiDigit(pathData[j]))
            {
                j++;
            }

            if (j + 1 < pathData.Length && pathData[j] == '.' && char.IsAsciiDigit(pathData[j + 1]))
            {
                var k = j + 1;
                while (k < pathData.Length && char.IsAsciiDigit(pathData[k]))
                {
                    k++;
                }

                var number = double.Parse(pathData.AsSpan(i, k - i), System.Globalization.CultureInfo.InvariantCulture);
                sanitized.Append(Js(JsRound(number)));
                i = k;
            }
            else
            {
                sanitized.Append(pathData, i, j - i);
                i = j;
            }
        }

        var text = sanitized.ToString();
        return text.Contains(roundedX, StringComparison.Ordinal) || text.Contains(roundedY, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Cuts a route where it enters a box (Mermaid's <c>cutPathAtIntersect</c>): the points before the first point
    ///     inside, then the intersection with the box's border.
    /// </summary>
    /// <param name="points">The route.</param>
    /// <param name="box">The box (a subgraph).</param>
    /// <returns>The cut route.</returns>
    internal static List<ShapePoint> CutPathAtIntersect(IReadOnlyList<ShapePoint> points, NodeBox box)
    {
        var result = new List<ShapePoint>();
        if (points.Count == 0)
        {
            return result;
        }

        var lastPointOutside = points[0];
        var isInside = false;
        foreach (var point in points)
        {
            if (!OutsideNode(box, point) && !isInside)
            {
                var intersection = Intersection(box, lastPointOutside, point);
                if (!result.Contains(intersection))
                {
                    result.Add(intersection);
                }

                isInside = true;
            }
            else
            {
                lastPointOutside = point;
                if (!isInside)
                {
                    result.Add(point);
                }
            }
        }

        return result;
    }

    /// <summary>
    ///     Intersects the segment from a point outside a box to a point inside with the box's border (Mermaid's
    ///     <c>intersection</c>).
    /// </summary>
    /// <param name="node">The box.</param>
    /// <param name="outsidePoint">The point outside.</param>
    /// <param name="insidePoint">The point inside.</param>
    /// <returns>The intersection.</returns>
    internal static ShapePoint Intersection(NodeBox node, ShapePoint outsidePoint, ShapePoint insidePoint)
    {
        var x = node.X;
        var y = node.Y;
        var w = node.Width / 2;
        var h = node.Height / 2;
        var q2 = Math.Abs(outsidePoint.Y - insidePoint.Y);
        var r2 = Math.Abs(outsidePoint.X - insidePoint.X);
        if (Math.Abs(y - outsidePoint.Y) * w > Math.Abs(x - outsidePoint.X) * h)
        {
            // The top or the bottom side
            var q = insidePoint.Y < outsidePoint.Y ? outsidePoint.Y - h - y : y - h - outsidePoint.Y;
            var r = r2 * q / q2;
            var resX = insidePoint.X < outsidePoint.X ? insidePoint.X + r : insidePoint.X - r2 + r;
            var resY = insidePoint.Y < outsidePoint.Y ? insidePoint.Y + q2 - q : insidePoint.Y - q2 + q;
            if (r == 0)
            {
                (resX, resY) = (outsidePoint.X, outsidePoint.Y);
            }

            if (r2 == 0)
            {
                resX = outsidePoint.X;
            }

            if (q2 == 0)
            {
                resY = outsidePoint.Y;
            }

            return new ShapePoint(resX, resY);
        }

        // The left or the right side
        var rs = insidePoint.X < outsidePoint.X ? outsidePoint.X - w - x : x - w - outsidePoint.X;
        var qs = q2 * rs / r2;
        var sideX = insidePoint.X < outsidePoint.X ? insidePoint.X + r2 - rs : insidePoint.X - r2 + rs;
        var sideY = insidePoint.Y < outsidePoint.Y ? insidePoint.Y + qs : insidePoint.Y - qs;
        if (rs == 0)
        {
            (sideX, sideY) = (outsidePoint.X, outsidePoint.Y);
        }

        if (r2 == 0)
        {
            sideX = outsidePoint.X;
        }

        if (q2 == 0)
        {
            sideY = outsidePoint.Y;
        }

        return new ShapePoint(sideX, sideY);
    }

    /// <summary>
    ///     Rounds off the right-angle corners of a route (Mermaid's <c>fixCorners</c>).
    /// </summary>
    /// <param name="lineData">The route.</param>
    /// <returns>The route with each corner replaced by three points.</returns>
    internal static List<ShapePoint> FixCorners(IReadOnlyList<ShapePoint> lineData)
    {
        var corners = new HashSet<int>();
        for (var i = 1; i < lineData.Count - 1; i++)
        {
            var (prev, curr, next) = (lineData[i - 1], lineData[i], lineData[i + 1]);
            var vertical = DagreLayout.Same(prev.X, curr.X) && DagreLayout.Same(curr.Y, next.Y) && Math.Abs(curr.X - next.X) > 5 && Math.Abs(curr.Y - prev.Y) > 5;
            var horizontal = DagreLayout.Same(prev.Y, curr.Y) && DagreLayout.Same(curr.X, next.X) && Math.Abs(curr.X - prev.X) > 5 && Math.Abs(curr.Y - next.Y) > 5;
            if (vertical || horizontal)
            {
                corners.Add(i);
            }
        }

        var result = new List<ShapePoint>();
        for (var i = 0; i < lineData.Count; i++)
        {
            if (!corners.Contains(i))
            {
                result.Add(lineData[i]);
                continue;
            }

            var (prev, corner, next) = (lineData[i - 1], lineData[i], lineData[i + 1]);
            var newPrev = FindAdjacentPoint(prev, corner, 5);
            var newNext = FindAdjacentPoint(next, corner, 5);
            var xDiff = newNext.X - newPrev.X;
            var yDiff = newNext.Y - newPrev.Y;
            result.Add(newPrev);
            var a = Math.Sqrt(2) * 2;
            var newCorner = corner;
            if (Math.Abs(next.X - prev.X) > 10 && Math.Abs(next.Y - prev.Y) >= 10)
            {
                const double R = 5;
                double cornerX;
                double cornerY;
                if (DagreLayout.Same(corner.X, newPrev.X))
                {
                    cornerX = xDiff < 0 ? newPrev.X - R + a : newPrev.X + R - a;
                    cornerY = yDiff < 0 ? newPrev.Y - a : newPrev.Y + a;
                }
                else
                {
                    cornerX = xDiff < 0 ? newPrev.X - a : newPrev.X + a;
                    cornerY = yDiff < 0 ? newPrev.Y - R + a : newPrev.Y + R - a;
                }

                newCorner = new ShapePoint(cornerX, cornerY);
            }

            result.Add(newCorner);
            result.Add(newNext);
        }

        return result;
    }

    /// <summary>
    ///     Draws the route with a d3 curve and the arrowhead offsets of <c>getLineFunctionsWithOffset</c>.
    /// </summary>
    /// <param name="data">The route.</param>
    /// <param name="arrowTypeStart">The arrow type at the start.</param>
    /// <param name="arrowTypeEnd">The arrow type at the end.</param>
    /// <param name="curve">The curve name.</param>
    /// <returns>The segments and the path data.</returns>
    internal static (IReadOnlyList<SceneSegment> Segments, string? Data) CurveWithOffsets(
        IReadOnlyList<ShapePoint> data,
        string arrowTypeStart,
        string arrowTypeEnd,
        string curve)
    {
        var path = Line(data.Count, i => OffsetX(data, i, arrowTypeStart, arrowTypeEnd), i => OffsetY(data, i, arrowTypeStart, arrowTypeEnd), curve);
        return (path.Segments, path.Data);
    }

    /// <summary>
    ///     Moves the first and last points of a route back by the arrowhead offsets, along the route (Mermaid's
    ///     <c>applyMarkerOffsetsToPoints</c>, used by the rounded curve).
    /// </summary>
    /// <param name="points">The route.</param>
    /// <param name="arrowTypeStart">The arrow type at the start.</param>
    /// <param name="arrowTypeEnd">The arrow type at the end.</param>
    /// <returns>The moved route.</returns>
    internal static List<ShapePoint> ApplyMarkerOffsetsToPoints(IReadOnlyList<ShapePoint> points, string arrowTypeStart, string arrowTypeEnd)
    {
        var result = points.ToList();
        if (points.Count >= 2 && MarkerOffsets.TryGetValue(arrowTypeStart, out var start) && start != 0)
        {
            var angle = Math.Atan2(points[1].Y - points[0].Y, points[1].X - points[0].X);
            result[0] = new ShapePoint(points[0].X + (start * Math.Cos(angle)), points[0].Y + (start * Math.Sin(angle)));
        }

        var n = points.Count;
        if (n >= 2 && MarkerOffsets.TryGetValue(arrowTypeEnd, out var end) && end != 0)
        {
            var angle = Math.Atan2(points[n - 1].Y - points[n - 2].Y, points[n - 1].X - points[n - 2].X);
            result[n - 1] = new ShapePoint(points[n - 1].X - (end * Math.Cos(angle)), points[n - 1].Y - (end * Math.Sin(angle)));
        }

        return result;
    }

    /// <summary>
    ///     Draws a route with rounded corners (Mermaid's <c>generateRoundedPath</c>): lines, and a quadratic curve at each
    ///     corner.
    /// </summary>
    /// <param name="points">The route.</param>
    /// <param name="radius">The corner radius.</param>
    /// <returns>The segments and the path data.</returns>
    internal static (IReadOnlyList<SceneSegment> Segments, string? Data) RoundedPath(IReadOnlyList<ShapePoint> points, double radius)
    {
        if (points.Count < 2)
        {
            return ([], null);
        }

        const double Epsilon = 1e-5;
        var segments = new List<SceneSegment>();
        var data = new StringBuilder();
        void Line(double x, double y)
        {
            data.Append('L').Append(Js(x)).Append(',').Append(Js(y));
            segments.Add(SceneSegment.Line(x, y));
        }

        for (var i = 0; i < points.Count; i++)
        {
            var curr = points[i];
            if (i == 0)
            {
                data.Append('M').Append(Js(curr.X)).Append(',').Append(Js(curr.Y));
                segments.Add(SceneSegment.Move(curr.X, curr.Y));
                continue;
            }

            if (i == points.Count - 1)
            {
                Line(curr.X, curr.Y);
                continue;
            }

            var (prev, next) = (points[i - 1], points[i + 1]);
            var (dx1, dy1, dx2, dy2) = (curr.X - prev.X, curr.Y - prev.Y, next.X - curr.X, next.Y - curr.Y);
            var len1 = Hypot(dx1, dy1);
            var len2 = Hypot(dx2, dy2);
            if (len1 < Epsilon || len2 < Epsilon)
            {
                Line(curr.X, curr.Y);
                continue;
            }

            var (nx1, ny1, nx2, ny2) = (dx1 / len1, dy1 / len1, dx2 / len2, dy2 / len2);
            var angle = Math.Acos(Math.Max(-1, Math.Min(1, (nx1 * nx2) + (ny1 * ny2))));
            if (angle < Epsilon || Math.Abs(Math.PI - angle) < Epsilon)
            {
                Line(curr.X, curr.Y);
                continue;
            }

            var cutLength = Math.Min(Math.Min(radius / Math.Sin(angle / 2), len1 / 2), len2 / 2);
            var (startX, startY) = (curr.X - (nx1 * cutLength), curr.Y - (ny1 * cutLength));
            var (endX, endY) = (curr.X + (nx2 * cutLength), curr.Y + (ny2 * cutLength));
            Line(startX, startY);
            data.Append('Q').Append(Js(curr.X)).Append(',').Append(Js(curr.Y)).Append(' ').Append(Js(endX)).Append(',').Append(Js(endY));
            segments.Add(SceneSegment.Quadratic(curr.X, curr.Y, endX, endY));
        }

        // A browser stops drawing at the first number that is not finite
        var finite = segments.TakeWhile(s => double.IsFinite(s.X) && double.IsFinite(s.Y) && double.IsFinite(s.X1) && double.IsFinite(s.Y1)).ToList();
        return (finite, data.ToString());
    }

    /// <summary>
    ///     Gets JavaScript's <c>Math.hypot</c> of two numbers.
    /// </summary>
    /// <param name="x">The first number.</param>
    /// <param name="y">The second number.</param>
    /// <returns>The length.</returns>
    private static double Hypot(double x, double y) => Math.Sqrt((x * x) + (y * y));

    /// <summary>
    ///     The x accessor of <c>getLineFunctionsWithOffset</c>: the first and last points pulled back by the arrowhead
    ///     offsets, and points too close to an end with an arrowhead pushed out of its way.
    /// </summary>
    /// <param name="data">The route.</param>
    /// <param name="i">The index of the point.</param>
    /// <param name="arrowTypeStart">The arrow type at the start.</param>
    /// <param name="arrowTypeEnd">The arrow type at the end.</param>
    /// <returns>The x.</returns>
    private static double OffsetX(IReadOnlyList<ShapePoint> data, int i, string arrowTypeStart, string arrowTypeEnd)
    {
        var d = data[i];
        var (first, last) = (data[0], data[^1]);
        var offset = 0.0;
        var direction = first.X < last.X ? "left" : "right";
        var hasStart = MarkerOffsets.TryGetValue(arrowTypeStart, out var startHeight);
        var hasEnd = MarkerOffsets.TryGetValue(arrowTypeEnd, out var endHeight);
        if (i == 0 && hasStart)
        {
            var (angle, deltaX, _) = DeltaAndAngle(data[0], data.Count > 1 ? data[1] : null);
            offset = startHeight * Math.Cos(angle) * (deltaX >= 0 ? 1 : -1);
        }
        else if (i == data.Count - 1 && hasEnd)
        {
            var (angle, deltaX, _) = DeltaAndAngle(data[^1], data.Count > 1 ? data[^2] : null);
            offset = endHeight * Math.Cos(angle) * (deltaX >= 0 ? 1 : -1);
        }

        var differenceToEnd = Math.Abs(d.X - last.X);
        var differenceInYEnd = Math.Abs(d.Y - last.Y);
        var differenceToStart = Math.Abs(d.X - first.X);
        var differenceInYStart = Math.Abs(d.Y - first.Y);
        if (hasEnd && differenceToEnd < endHeight && differenceToEnd > 0 && differenceInYEnd < endHeight)
        {
            offset -= (endHeight + 1 - differenceToEnd) * (direction == "right" ? -1 : 1);
        }

        if (hasStart && differenceToStart < startHeight && differenceToStart > 0 && differenceInYStart < startHeight)
        {
            offset += (startHeight + 1 - differenceToStart) * (direction == "right" ? -1 : 1);
        }

        return d.X + offset;
    }

    /// <summary>
    ///     The y accessor of <c>getLineFunctionsWithOffset</c>.
    /// </summary>
    /// <param name="data">The route.</param>
    /// <param name="i">The index of the point.</param>
    /// <param name="arrowTypeStart">The arrow type at the start.</param>
    /// <param name="arrowTypeEnd">The arrow type at the end.</param>
    /// <returns>The y.</returns>
    private static double OffsetY(IReadOnlyList<ShapePoint> data, int i, string arrowTypeStart, string arrowTypeEnd)
    {
        var d = data[i];
        var (first, last) = (data[0], data[^1]);
        var offset = 0.0;
        var direction = first.Y < last.Y ? "down" : "up";
        var hasStart = MarkerOffsets.TryGetValue(arrowTypeStart, out var startHeight);
        var hasEnd = MarkerOffsets.TryGetValue(arrowTypeEnd, out var endHeight);
        if (i == 0 && hasStart)
        {
            var (angle, _, deltaY) = DeltaAndAngle(data[0], data.Count > 1 ? data[1] : null);
            offset = startHeight * Math.Abs(Math.Sin(angle)) * (deltaY >= 0 ? 1 : -1);
        }
        else if (i == data.Count - 1 && hasEnd)
        {
            var (angle, _, deltaY) = DeltaAndAngle(data[^1], data.Count > 1 ? data[^2] : null);
            offset = endHeight * Math.Abs(Math.Sin(angle)) * (deltaY >= 0 ? 1 : -1);
        }

        var differenceToEnd = Math.Abs(d.Y - last.Y);
        var differenceInXEnd = Math.Abs(d.X - last.X);
        var differenceToStart = Math.Abs(d.Y - first.Y);
        var differenceInXStart = Math.Abs(d.X - first.X);
        if (hasEnd && differenceToEnd < endHeight && differenceToEnd > 0 && differenceInXEnd < endHeight)
        {
            offset -= (endHeight + 1 - differenceToEnd) * (direction == "up" ? -1 : 1);
        }

        if (hasStart && differenceToStart < startHeight && differenceToStart > 0 && differenceInXStart < startHeight)
        {
            offset += (startHeight + 1 - differenceToStart) * (direction == "up" ? -1 : 1);
        }

        return d.Y + offset;
    }

    /// <summary>
    ///     The angle and the deltas between two points (<c>lineWithOffset.ts</c>'s <c>calculateDeltaAndAngle</c>, which
    ///     uses <c>Math.atan</c> of the slope).
    /// </summary>
    /// <param name="point1">The first point.</param>
    /// <param name="point2">The second point, or null (no angle).</param>
    /// <returns>The angle and the deltas.</returns>
    private static (double Angle, double DeltaX, double DeltaY) DeltaAndAngle(ShapePoint point1, ShapePoint? point2)
    {
        if (point2 is not { } p2)
        {
            return (0, 0, 0);
        }

        var deltaX = p2.X - point1.X;
        var deltaY = p2.Y - point1.Y;
        return (Math.Atan(deltaY / deltaX), deltaX, deltaY);
    }

    /// <summary>
    ///     Gets the point at a distance before another point on the line between two points (Mermaid's
    ///     <c>findAdjacentPoint</c>).
    /// </summary>
    /// <param name="pointA">The far point.</param>
    /// <param name="pointB">The corner.</param>
    /// <param name="distance">The distance.</param>
    /// <returns>The point.</returns>
    private static ShapePoint FindAdjacentPoint(ShapePoint pointA, ShapePoint pointB, double distance)
    {
        var xDiff = pointB.X - pointA.X;
        var yDiff = pointB.Y - pointA.Y;
        var length = Math.Sqrt((xDiff * xDiff) + (yDiff * yDiff));
        var ratio = distance / length;
        return new ShapePoint(pointB.X - (ratio * xDiff), pointB.Y - (ratio * yDiff));
    }

    /// <summary>
    ///     Gets whether a point is outside a box (Mermaid's <c>outsideNode</c>; on the border counts as outside).
    /// </summary>
    /// <param name="node">The box.</param>
    /// <param name="point">The point.</param>
    /// <returns>Whether it is outside.</returns>
    private static bool OutsideNode(NodeBox node, ShapePoint point) =>
        Math.Abs(point.X - node.X) >= node.Width / 2 || Math.Abs(point.Y - node.Y) >= node.Height / 2;

    /// <summary>
    ///     Gets the distance between two points.
    /// </summary>
    /// <param name="a">The first point.</param>
    /// <param name="b">The second point.</param>
    /// <returns>The distance.</returns>
    private static double Distance(ShapePoint a, ShapePoint b) => Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));

    /// <summary>
    ///     Rounds as Mermaid's <c>roundNumber</c> does (<c>Math.round(num * 10^precision) / 10^precision</c>).
    /// </summary>
    /// <param name="value">The number.</param>
    /// <param name="precision">The decimals.</param>
    /// <returns>The rounded number.</returns>
    private static double RoundNumber(double value, int precision)
    {
        var factor = Math.Pow(10, precision);
        return JsRound(value * factor) / factor;
    }

    /// <summary>
    ///     JavaScript's <c>Math.round</c> (halves round up).
    /// </summary>
    /// <param name="value">The number.</param>
    /// <returns>The rounded number.</returns>
    private static double JsRound(double value) => Math.Floor(value + 0.5);

    /// <summary>
    ///     The segments of a rectangle.
    /// </summary>
    /// <param name="box">The rectangle.</param>
    /// <returns>The segments.</returns>
    private static List<SceneSegment> RectSegments(SceneRect box) =>
    [
        SceneSegment.Move(box.X, box.Y),
        SceneSegment.Line(box.Right, box.Y),
        SceneSegment.Line(box.Right, box.Bottom),
        SceneSegment.Line(box.X, box.Bottom),
        SceneSegment.Close(),
    ];

    /// <summary>
    ///     Resolves the style of an edge's path: the style sheet's rules for its classes in sheet order, then its inline
    ///     style (the class styles of its <c>classDef</c>s and its own styles), and the stroke color its markers take.
    /// </summary>
    /// <param name="edge">The edge.</param>
    /// <param name="context">The settings.</param>
    /// <returns>The style and the stroke text of the marker color (Mermaid's <c>/stroke:([^;]+)/</c> match), or null.</returns>
    private static (StyleDeclarationSet Style, string? StrokeText) PathStyle(EdgeRoute edge, ShapeContext context)
    {
        var data = edge.Data;
        var classes = new HashSet<string>(StringComparer.Ordinal)
        {
            data.Thickness switch { "thick" => "edge-thickness-thick", "invisible" => "edge-thickness-invisible", _ => "edge-thickness-normal" },
            data.Pattern switch { "dotted" => "edge-pattern-dotted", "dashed" => "edge-pattern-dashed", _ => "edge-pattern-solid" },
        };
        foreach (var name in data.Classes.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            classes.Add(name);
        }

        if (data.Animation is { IsTruthy: true } named)
        {
            classes.Add("edge-animation-" + named.ToJsString());
        }
        else if (data.Animate is { IsTruthy: true })
        {
            classes.Add("edge-animation-fast");
        }

        // The sheet's rules for the path's classes, over the fill it inherits from the root
        var style = new StyleDeclarationSet(
            context.Styles.Resolve(FlowchartStyles.Root)["fill"] is { } inherited ? [new StyleDeclaration("fill", inherited, false)] : []);
        foreach (var rule in context.Styles.Rules)
        {
            if (rule.Selector.Length > 1 && rule.Selector[0] == '.' && classes.Contains(rule.Selector[1..]))
            {
                style = style.Cascade(rule.Declarations);
            }
        }

        // The inline style: the class styles, then the edge's styles (written twice by Mermaid)
        List<string> inline = [.. data.CssCompiledStyles, .. data.Style, .. data.Style];
        style = style.Cascade(StyleDeclarations.ParseItems(inline, data.Location, context.Sink));

        // pathStyle as Mermaid writes it, searched for the first stroke declaration
        var classStyles = string.Join(";", data.CssCompiledStyles);
        var styles = string.Concat(data.Style.Select(s => s + ";"));
        var pathStyle = (classStyles.Length > 0 ? classStyles + ";" + styles + ";" : styles) + ";" + string.Concat(data.Style.Select(s => ";" + s));
        var match = StrokePattern.Match(pathStyle);
        return (style, match.Success ? match.Groups[1].Value : null);
    }

    /// <summary>
    ///     Makes the path item of an edge with its resolved style.
    /// </summary>
    /// <param name="segments">The geometry.</param>
    /// <param name="style">The style.</param>
    /// <param name="id">The edge id.</param>
    /// <param name="location">The location of the edge.</param>
    /// <returns>The path.</returns>
    private static ScenePath PathOf(IReadOnlyList<SceneSegment> segments, StyleDeclarationSet style, string id, MermaidLocation location)
    {
        var opacity = style["opacity"] is { } text && StyleDeclarations.TryParseOpacity(text, out var factor) ? factor : 1;
        Rgba32? Fade(Rgba32? color) =>
            color is { } c && opacity < 1 ? c with { A = (byte)Math.Round(c.A * Math.Max(opacity, 0), MidpointRounding.AwayFromZero) } : color;
        var width = style.Length("stroke-width", 1);
        var dash = style["stroke-dasharray"] is { } dashes && StyleDeclarations.TryParseDashArray(dashes, out var parsed) && parsed.Count > 0
            ? parsed
            : null;
        var stroke = Fade(style.Paint("stroke")) is { } color && width > 0
            ? new SceneStroke(color, width) { Dash = dash, DashOffset = style.Length("stroke-dashoffset", 0) }
            : null;
        return new ScenePath(segments) { Role = "edge", Id = id, Location = location, Fill = Fade(style.Paint("fill")), Stroke = stroke };
    }

    /// <summary>
    ///     Gets the paint of a marker shape: the style sheet's <c>.marker</c> rule (and <c>.marker.cross</c> for the
    ///     cross), or the edge's stroke color for the stroke and, for the filled point, the fill.
    /// </summary>
    /// <param name="shape">The marker shape.</param>
    /// <param name="strokeText">The edge's stroke color as written, or null.</param>
    /// <param name="styles">The style sheet.</param>
    /// <returns>The paint.</returns>
    private static MarkerPaint MarkerPaintOf(string shape, string? strokeText, FlowchartStyles styles)
    {
        var rule = styles.Resolve(".marker");
        if (shape == "cross")
        {
            rule = rule.Cascade(styles.Resolve(".marker.cross"));
        }

        // A marker inherits the root's fill when its rule sets none
        var rootFill = styles.Resolve(FlowchartStyles.Root).Paint("fill");
        var fill = rule["fill"] is not null ? rule.Paint("fill") : rootFill;
        var stroke = rule.Paint("stroke");
        if (strokeText is { } text && text.Trim().Length > 0 && StyleDeclarations.TryParsePaint(text.Trim(), out var color))
        {
            stroke = color;
            if (shape == "point")
            {
                fill = color;
            }
        }

        return new MarkerPaint(fill, stroke);
    }
}
