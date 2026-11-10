using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Mermaid.Scenegraph;

/// <summary>
///     An axis-aligned rectangle in scene units (Mermaid's SVG user units, which are CSS pixels at scale 1).
/// </summary>
/// <param name="X">The left edge.</param>
/// <param name="Y">The top edge.</param>
/// <param name="Width">The width; not negative.</param>
/// <param name="Height">The height; not negative.</param>
internal readonly record struct SceneRect(double X, double Y, double Width, double Height)
{
    /// <summary>
    ///     Gets the right edge.
    /// </summary>
    public double Right => X + Width;

    /// <summary>
    ///     Gets the bottom edge.
    /// </summary>
    public double Bottom => Y + Height;

    /// <summary>
    ///     Gets the smallest rectangle that contains both rectangles.
    /// </summary>
    /// <param name="other">The other rectangle.</param>
    /// <returns>The union.</returns>
    public SceneRect Union(SceneRect other)
    {
        var x = Math.Min(X, other.X);
        var y = Math.Min(Y, other.Y);
        return new SceneRect(x, y, Math.Max(Right, other.Right) - x, Math.Max(Bottom, other.Bottom) - y);
    }

    /// <summary>
    ///     Gets the rectangle moved by an offset.
    /// </summary>
    /// <param name="dx">The horizontal offset.</param>
    /// <param name="dy">The vertical offset.</param>
    /// <returns>The moved rectangle.</returns>
    public SceneRect Offset(double dx, double dy) => new(X + dx, Y + dy, Width, Height);
}

/// <summary>
///     The kind of one segment of a <see cref="ScenePath"/>.
/// </summary>
internal enum SceneSegmentKind
{
    /// <summary>Starts a new subpath at the point.</summary>
    MoveTo,

    /// <summary>A straight line to the point.</summary>
    LineTo,

    /// <summary>A quadratic Bezier curve through one control point to the point.</summary>
    QuadraticTo,

    /// <summary>A cubic Bezier curve through two control points to the point.</summary>
    CubicTo,

    /// <summary>An SVG elliptical arc to the point.</summary>
    ArcTo,

    /// <summary>Closes the current subpath.</summary>
    Close,
}

/// <summary>
///     One segment of the geometry of a <see cref="ScenePath"/>: the SVG path commands Mermaid's shapes and edges use
///     (<c>M L Q C A Z</c>), in absolute scene units.
/// </summary>
/// <remarks>
///     The fields are reused per kind: <see cref="X"/> and <see cref="Y"/> are always the end point; a quadratic curve has its
///     control point in <see cref="X1"/> and <see cref="Y1"/>, a cubic curve its two control points in <see cref="X1"/> to
///     <see cref="Y2"/>, and an arc its radii in <see cref="X1"/> and <see cref="Y1"/> and its rotation in degrees in
///     <see cref="X2"/>. Use the factory methods instead of the constructor.
/// </remarks>
/// <param name="Kind">The kind of segment.</param>
/// <param name="X">The end point's x.</param>
/// <param name="Y">The end point's y.</param>
/// <param name="X1">The first control point's x, or the arc's x radius.</param>
/// <param name="Y1">The first control point's y, or the arc's y radius.</param>
/// <param name="X2">The second control point's x, or the arc's rotation in degrees.</param>
/// <param name="Y2">The second control point's y.</param>
/// <param name="LargeArc">The arc's large-arc flag.</param>
/// <param name="Sweep">The arc's sweep flag.</param>
internal readonly record struct SceneSegment(
    SceneSegmentKind Kind,
    double X,
    double Y,
    double X1 = 0,
    double Y1 = 0,
    double X2 = 0,
    double Y2 = 0,
    bool LargeArc = false,
    bool Sweep = false)
{
    /// <summary>Creates a move to a point.</summary>
    /// <param name="x">The x.</param>
    /// <param name="y">The y.</param>
    /// <returns>The segment.</returns>
    public static SceneSegment Move(double x, double y) => new(SceneSegmentKind.MoveTo, x, y);

    /// <summary>Creates a line to a point.</summary>
    /// <param name="x">The x.</param>
    /// <param name="y">The y.</param>
    /// <returns>The segment.</returns>
    public static SceneSegment Line(double x, double y) => new(SceneSegmentKind.LineTo, x, y);

    /// <summary>Creates a quadratic curve.</summary>
    /// <param name="cx">The control point's x.</param>
    /// <param name="cy">The control point's y.</param>
    /// <param name="x">The end point's x.</param>
    /// <param name="y">The end point's y.</param>
    /// <returns>The segment.</returns>
    public static SceneSegment Quadratic(double cx, double cy, double x, double y) => new(SceneSegmentKind.QuadraticTo, x, y, cx, cy);

    /// <summary>Creates a cubic curve.</summary>
    /// <param name="c1x">The first control point's x.</param>
    /// <param name="c1y">The first control point's y.</param>
    /// <param name="c2x">The second control point's x.</param>
    /// <param name="c2y">The second control point's y.</param>
    /// <param name="x">The end point's x.</param>
    /// <param name="y">The end point's y.</param>
    /// <returns>The segment.</returns>
    public static SceneSegment Cubic(double c1x, double c1y, double c2x, double c2y, double x, double y) =>
        new(SceneSegmentKind.CubicTo, x, y, c1x, c1y, c2x, c2y);

    /// <summary>Creates an SVG elliptical arc.</summary>
    /// <param name="rx">The x radius.</param>
    /// <param name="ry">The y radius.</param>
    /// <param name="rotation">The rotation of the ellipse in degrees.</param>
    /// <param name="largeArc">The large-arc flag.</param>
    /// <param name="sweep">The sweep flag.</param>
    /// <param name="x">The end point's x.</param>
    /// <param name="y">The end point's y.</param>
    /// <returns>The segment.</returns>
    public static SceneSegment Arc(double rx, double ry, double rotation, bool largeArc, bool sweep, double x, double y) =>
        new(SceneSegmentKind.ArcTo, x, y, rx, ry, rotation, 0, largeArc, sweep);

    /// <summary>Creates a close of the current subpath.</summary>
    /// <returns>The segment.</returns>
    public static SceneSegment Close() => new(SceneSegmentKind.Close, 0, 0);
}

/// <summary>
///     The stroke of a <see cref="ScenePath"/>: Mermaid's <c>stroke</c>, <c>stroke-width</c> and <c>stroke-dasharray</c>
///     with the opacity folded into the color.
/// </summary>
/// <param name="Color">The color; a fully transparent color draws nothing.</param>
/// <param name="Width">The width in scene units; zero or less draws nothing.</param>
internal sealed record SceneStroke(Rgba32 Color, double Width)
{
    /// <summary>
    ///     Gets the dash lengths in scene units, or null for a solid line.
    /// </summary>
    public IReadOnlyList<double>? Dash { get; init; }

    /// <summary>
    ///     Gets the offset into the dash pattern in scene units.
    /// </summary>
    public double DashOffset { get; init; }

    /// <summary>
    ///     Gets the line cap; <see cref="LineCap.Butt"/> by default (SVG's default).
    /// </summary>
    public LineCap Cap { get; init; } = LineCap.Butt;

    /// <summary>
    ///     Gets the line join; <see cref="LineJoin.Miter"/> by default (SVG's default).
    /// </summary>
    public LineJoin Join { get; init; } = LineJoin.Miter;
}

/// <summary>
///     A drop shadow drawn under a <see cref="ScenePath"/>: CSS's <c>filter: drop-shadow(...)</c>, the blurred
///     silhouette of the path's fill and stroke, moved and painted in one color.
/// </summary>
/// <param name="OffsetX">The horizontal offset in scene units.</param>
/// <param name="OffsetY">The vertical offset in scene units.</param>
/// <param name="Blur">The standard deviation of the Gaussian blur in scene units; zero for a sharp shadow.</param>
/// <param name="Color">The color; its alpha scales the silhouette's coverage.</param>
internal sealed record SceneShadow(double OffsetX, double OffsetY, double Blur, Rgba32 Color);

/// <summary>
///     The font of a <see cref="SceneTextRun"/>: a CSS family list, a size and a style.
/// </summary>
/// <param name="Family">The CSS <c>font-family</c> list (resolved to bundled fonts by the painter).</param>
/// <param name="Size">The font size in scene units.</param>
/// <param name="Bold">Whether the run is bold.</param>
/// <param name="Italic">Whether the run is italic.</param>
internal sealed record SceneFont(string Family, double Size, bool Bold, bool Italic);

/// <summary>
///     A run of text with one font and color, part of a <see cref="SceneText"/> line.
/// </summary>
/// <param name="Text">The text.</param>
/// <param name="Font">The font.</param>
/// <param name="Color">The color, with the opacity folded in.</param>
internal sealed record SceneTextRun(string Text, SceneFont Font, Rgba32 Color)
{
    /// <summary>
    ///     Gets a value indicating whether the run is underlined (CSS's <c>text-decoration: underline</c>, which a class
    ///     diagram gives a static member); false by default.
    /// </summary>
    public bool Underline { get; init; }
}

/// <summary>
///     Where the anchor of a text line is: SVG's <c>text-anchor</c>.
/// </summary>
internal enum SceneTextAnchor
{
    /// <summary>The line starts at the anchor.</summary>
    Start,

    /// <summary>The line is centered on the anchor.</summary>
    Middle,

    /// <summary>The line ends at the anchor.</summary>
    End,
}

/// <summary>
///     One item of a scene, with the role and id that tests and the reference comparison use to find it.
/// </summary>
/// <remarks>
///     Items are immutable records; the role is a stable name such as <c>node</c>, <c>edge</c>, <c>edge-label</c>,
///     <c>cluster</c>, <c>marker</c> or <c>label</c>, and the id is Mermaid's element id where there is one.
/// </remarks>
internal abstract record SceneItem
{
    /// <summary>
    ///     Gets the role of the item.
    /// </summary>
    public required string Role { get; init; }

    /// <summary>
    ///     Gets Mermaid's id of the element the item draws, or null.
    /// </summary>
    public string? Id { get; init; }

    /// <summary>
    ///     Gets the location of the construct in the original text that the item draws; unknown by default.
    /// </summary>
    public MermaidLocation Location { get; init; }
}

/// <summary>
///     A group of items translated together (SVG's <c>&lt;g transform="translate(x, y)"&gt;</c>).
/// </summary>
/// <param name="OffsetX">The horizontal translation of the children.</param>
/// <param name="OffsetY">The vertical translation of the children.</param>
/// <param name="Children">The children, drawn in order.</param>
internal sealed record SceneGroup(double OffsetX, double OffsetY, IReadOnlyList<SceneItem> Children) : SceneItem;

/// <summary>
///     A filled and/or stroked path.
/// </summary>
/// <param name="Segments">The geometry.</param>
internal sealed record ScenePath(IReadOnlyList<SceneSegment> Segments) : SceneItem
{
    /// <summary>
    ///     Gets the fill color with the opacity folded in, or null for no fill.
    /// </summary>
    public Rgba32? Fill { get; init; }

    /// <summary>
    ///     Gets the fill rule; <see cref="FillRule.NonZero"/> by default (SVG's default).
    /// </summary>
    public FillRule FillRule { get; init; } = FillRule.NonZero;

    /// <summary>
    ///     Gets the stroke, or null for no stroke.
    /// </summary>
    public SceneStroke? Stroke { get; init; }

    /// <summary>
    ///     Gets the drop shadow painted under the path, or null for none.
    /// </summary>
    public SceneShadow? Shadow { get; init; }
}

/// <summary>
///     One line of text: runs drawn one after the other on a baseline.
/// </summary>
/// <param name="X">The x of the anchor.</param>
/// <param name="Baseline">The y of the baseline.</param>
/// <param name="Anchor">Where the anchor is on the line.</param>
/// <param name="Runs">The runs in order.</param>
/// <param name="Box">The box of the line as the label layout measured it (for the bounds of the scene and for tests).</param>
internal sealed record SceneText(double X, double Baseline, SceneTextAnchor Anchor, IReadOnlyList<SceneTextRun> Runs, SceneRect Box) : SceneItem
{
    /// <summary>
    ///     Gets the stroke of the glyph outlines, drawn over their fill, or null for none (most text; a sequence label of
    ///     the <c>old</c> text placement is stroked by its style rule).
    /// </summary>
    public SceneStroke? Stroke { get; init; }
}

/// <summary>
///     A picture drawn into a box (an <c>@{ img }</c> node or an <c>&lt;img&gt;</c> in a label).
/// </summary>
/// <param name="Box">The box the picture fills.</param>
/// <param name="Href">The link the picture loader resolves.</param>
internal sealed record SceneImage(SceneRect Box, string Href) : SceneItem
{
    /// <summary>
    ///     Gets the opacity from 0 to 1; 1 by default.
    /// </summary>
    public double Opacity { get; init; } = 1;
}

/// <summary>
///     A drawing of a diagram: the items in paint order and the view box that the surface shows (Mermaid's SVG
///     <c>viewBox</c>, whose size is the natural size of the diagram).
/// </summary>
/// <param name="ViewBox">The part of the scene the picture shows.</param>
/// <param name="Items">The items in paint order.</param>
internal sealed record Scene(SceneRect ViewBox, IReadOnlyList<SceneItem> Items)
{
    /// <summary>
    ///     Gets the picture loader of the scene's pictures (the one that measured them during the layout, so the image
    ///     budget is shared), or null when the scene has none.
    /// </summary>
    public ISceneImageLoader? Images { get; init; }

    /// <summary>
    ///     Enumerates every item of the scene depth first in paint order, with the accumulated translation of the groups
    ///     around it (an item's absolute position is its own coordinates plus the offset).
    /// </summary>
    /// <returns>The items and their offsets.</returns>
    public IEnumerable<(SceneItem Item, double OffsetX, double OffsetY)> Walk() => Walk(Items, 0, 0);

    /// <summary>
    ///     Enumerates items depth first with their accumulated offsets.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <param name="dx">The offset of the items.</param>
    /// <param name="dy">The offset of the items.</param>
    /// <returns>The items and their offsets.</returns>
    private static IEnumerable<(SceneItem Item, double OffsetX, double OffsetY)> Walk(IReadOnlyList<SceneItem> items, double dx, double dy)
    {
        foreach (var item in items)
        {
            yield return (item, dx, dy);
            if (item is SceneGroup group)
            {
                foreach (var child in Walk(group.Children, dx + group.OffsetX, dy + group.OffsetY))
                {
                    yield return child;
                }
            }
        }
    }

    /// <summary>
    ///     Gets the bounds of items, groups' translations applied: the union of the geometry of every path (curves exactly,
    ///     strokes not included, as SVG's <c>getBBox</c>), the box of every text line and of every picture.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <returns>The bounds, or null when the items draw nothing.</returns>
    public static SceneRect? BoundsOf(IReadOnlyList<SceneItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        return BoundsOf(items, 0, 0);
    }

    /// <summary>
    ///     Builds the core path of segments, transformed into device space.
    /// </summary>
    /// <param name="segments">The segments.</param>
    /// <param name="scale">The uniform scale.</param>
    /// <param name="offsetX">The translation after scaling, x.</param>
    /// <param name="offsetY">The translation after scaling, y.</param>
    /// <returns>The path; segments before the first move start at the origin as in SVG.</returns>
    public static Path ToPath(IReadOnlyList<SceneSegment> segments, double scale, double offsetX, double offsetY)
    {
        ArgumentNullException.ThrowIfNull(segments);
        var builder = new PathBuilder();
        Vector2 Map(double x, double y) => new((float)((x * scale) + offsetX), (float)((y * scale) + offsetY));
        var started = false;
        foreach (var segment in segments)
        {
            // A drawing command without a current point starts at the origin (as an SVG path that does not begin with M)
            if (!started && segment.Kind != SceneSegmentKind.MoveTo && segment.Kind != SceneSegmentKind.Close)
            {
                builder.MoveTo(Map(0, 0));
            }

            started = true;
            switch (segment.Kind)
            {
                case SceneSegmentKind.MoveTo:
                    builder.MoveTo(Map(segment.X, segment.Y));
                    break;
                case SceneSegmentKind.LineTo:
                    builder.LineTo(Map(segment.X, segment.Y));
                    break;
                case SceneSegmentKind.QuadraticTo:
                    builder.QuadraticBezierTo(Map(segment.X1, segment.Y1), Map(segment.X, segment.Y));
                    break;
                case SceneSegmentKind.CubicTo:
                    builder.CubicBezierTo(Map(segment.X1, segment.Y1), Map(segment.X2, segment.Y2), Map(segment.X, segment.Y));
                    break;
                case SceneSegmentKind.ArcTo:
                    builder.ArcTo(
                        new Vector2((float)Math.Abs(segment.X1 * scale), (float)Math.Abs(segment.Y1 * scale)),
                        (float)segment.X2,
                        segment.LargeArc,
                        segment.Sweep,
                        Map(segment.X, segment.Y));
                    break;
                default:
                    builder.Close();
                    break;
            }
        }

        return builder.Build();
    }

    /// <summary>
    ///     Gets the bounds of items with an accumulated translation.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <param name="dx">The accumulated horizontal translation.</param>
    /// <param name="dy">The accumulated vertical translation.</param>
    /// <returns>The bounds, or null.</returns>
    private static SceneRect? BoundsOf(IReadOnlyList<SceneItem> items, double dx, double dy)
    {
        SceneRect? bounds = null;
        foreach (var item in items)
        {
            var itemBounds = item switch
            {
                SceneGroup group => BoundsOf(group.Children, dx + group.OffsetX, dy + group.OffsetY),
                ScenePath path => PathBounds(path.Segments, dx, dy),
                SceneText text => text.Box.Offset(dx, dy),
                SceneImage image => image.Box.Offset(dx, dy),
                _ => null,
            };

            if (itemBounds is { } b)
            {
                bounds = bounds is { } current ? current.Union(b) : b;
            }
        }

        return bounds;
    }

    /// <summary>
    ///     Gets the bounds of path geometry.
    /// </summary>
    /// <param name="segments">The segments.</param>
    /// <param name="dx">The horizontal translation.</param>
    /// <param name="dy">The vertical translation.</param>
    /// <returns>The bounds, or null for an empty path.</returns>
    private static SceneRect? PathBounds(IReadOnlyList<SceneSegment> segments, double dx, double dy)
    {
        if (segments.Count == 0)
        {
            return null;
        }

        // The curves are flattened finely, so the bounds are those of the curve, not of its control points
        var rect = ToPath(segments, 1, dx, dy).GetBounds(0.01f);
        return rect.IsEmpty ? null : new SceneRect(rect.X, rect.Y, rect.Width, rect.Height);
    }
}
