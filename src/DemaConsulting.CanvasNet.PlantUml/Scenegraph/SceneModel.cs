using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.PlantUml.Scenegraph;

/// <summary>
///     An axis-aligned rectangle in scene units (PlantUML's SVG user units, which are pixels at scale 1).
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

    /// <summary>A cubic Bezier curve through two control points to the point.</summary>
    CubicTo,

    /// <summary>Closes the current subpath.</summary>
    Close,
}

/// <summary>
///     One segment of the geometry of a <see cref="ScenePath"/>, in absolute scene units.
/// </summary>
/// <remarks>
///     <see cref="X"/> and <see cref="Y"/> are the end point; a cubic curve has its two control points in <see cref="X1"/>
///     to <see cref="Y2"/>. Use the factory methods instead of the constructor.
/// </remarks>
/// <param name="Kind">The kind of segment.</param>
/// <param name="X">The end point's x.</param>
/// <param name="Y">The end point's y.</param>
/// <param name="X1">The first control point's x.</param>
/// <param name="Y1">The first control point's y.</param>
/// <param name="X2">The second control point's x.</param>
/// <param name="Y2">The second control point's y.</param>
internal readonly record struct SceneSegment(SceneSegmentKind Kind, double X, double Y, double X1 = 0, double Y1 = 0, double X2 = 0, double Y2 = 0)
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

    /// <summary>Creates a close of the current subpath.</summary>
    /// <returns>The segment.</returns>
    public static SceneSegment Close() => new(SceneSegmentKind.Close, 0, 0);
}

/// <summary>
///     The stroke of a <see cref="ScenePath"/>: a color, a width and an optional dash pattern.
/// </summary>
/// <param name="Color">The color; a fully transparent color draws nothing.</param>
/// <param name="Width">The width in scene units; zero or less draws nothing.</param>
internal sealed record SceneStroke(Rgba32 Color, double Width)
{
    /// <summary>
    ///     Gets the dash lengths in scene units, or null for a solid line.
    /// </summary>
    public IReadOnlyList<double>? Dash { get; init; }
}

/// <summary>
///     The font of a <see cref="SceneTextRun"/>: a PlantUML family name, a size and a style.
/// </summary>
/// <param name="Family">The family name as PlantUML writes it (mapped to a bundled font by the text layout).</param>
/// <param name="Size">The font size in scene units.</param>
/// <param name="Bold">Whether the run is bold.</param>
/// <param name="Italic">Whether the run is italic.</param>
internal sealed record SceneFont(string Family, double Size, bool Bold, bool Italic);

/// <summary>
///     A run of text with one font and color, part of a <see cref="SceneText"/> line.
/// </summary>
/// <param name="Text">The text.</param>
/// <param name="Font">The font.</param>
/// <param name="Color">The color.</param>
/// <param name="Width">The advance of the run in scene units, as the text layout measured it.</param>
internal sealed record SceneTextRun(string Text, SceneFont Font, Rgba32 Color, double Width)
{
    /// <summary>
    ///     Gets a value indicating whether the run is underlined.
    /// </summary>
    public bool Underline { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the run is struck through.
    /// </summary>
    public bool Strike { get; init; }
}

/// <summary>
///     One item of a scene, with the role that tests and the reference comparison use to find it.
/// </summary>
/// <remarks>
///     Items are immutable records; the role is a stable name such as <c>participant</c>, <c>lifeline</c>,
///     <c>message</c>, <c>note</c>, <c>group</c>, or <c>title</c>.
/// </remarks>
internal abstract record SceneItem
{
    /// <summary>
    ///     Gets the role of the item.
    /// </summary>
    public required string Role { get; init; }

    /// <summary>
    ///     Gets the id of the element the item draws (a participant's code), or null.
    /// </summary>
    public string? Id { get; init; }

    /// <summary>
    ///     Gets the location of the construct in the original text that the item draws; unknown by default.
    /// </summary>
    public PlantUmlLocation Location { get; init; }
}

/// <summary>
///     A filled and/or stroked path.
/// </summary>
/// <param name="Segments">The geometry.</param>
internal sealed record ScenePath(IReadOnlyList<SceneSegment> Segments) : SceneItem
{
    /// <summary>
    ///     Gets the fill color, or null for no fill.
    /// </summary>
    public Rgba32? Fill { get; init; }

    /// <summary>
    ///     Gets the stroke, or null for no stroke.
    /// </summary>
    public SceneStroke? Stroke { get; init; }
}

/// <summary>
///     One line of text: runs drawn one after the other on a baseline, starting at <see cref="X"/>.
/// </summary>
/// <param name="X">The x where the first run starts.</param>
/// <param name="Baseline">The y of the baseline.</param>
/// <param name="Runs">The runs in order.</param>
/// <param name="Box">The box of the line as the text layout measured it (for the bounds of the scene and for tests).</param>
internal sealed record SceneText(double X, double Baseline, IReadOnlyList<SceneTextRun> Runs, SceneRect Box) : SceneItem
{
    /// <summary>
    ///     Gets the text of the line: its runs joined.
    /// </summary>
    public string Text => string.Concat(Runs.Select(r => r.Text));
}

/// <summary>
///     A drawing of a diagram: the items in paint order, the view box that the surface shows (whose size is the
///     natural size of the diagram), and the background.
/// </summary>
/// <param name="ViewBox">The part of the scene the picture shows.</param>
/// <param name="Items">The items in paint order.</param>
internal sealed record Scene(SceneRect ViewBox, IReadOnlyList<SceneItem> Items)
{
    /// <summary>
    ///     Gets the background of the diagram, or null for transparent; null by default.
    /// </summary>
    public Rgba32? Background { get; init; }

    /// <summary>
    ///     Gets the diagram's own scale factor (its <c>scale</c> statement), which multiplies the scale of the call; 1 by
    ///     default.
    /// </summary>
    public double Scale { get; init; } = 1;

    /// <summary>
    ///     Gets the bounds of items: the union of the points of every path (strokes not included) and the box of every
    ///     text line.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <returns>The bounds, or null when the items draw nothing.</returns>
    public static SceneRect? BoundsOf(IEnumerable<SceneItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        SceneRect? bounds = null;
        foreach (var item in items)
        {
            var itemBounds = item switch
            {
                ScenePath path => PathBounds(path.Segments),
                SceneText text => text.Box,
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
    ///     Builds the core path of segments, transformed into device space.
    /// </summary>
    /// <param name="segments">The segments.</param>
    /// <param name="scale">The uniform scale.</param>
    /// <param name="offsetX">The translation after scaling, x.</param>
    /// <param name="offsetY">The translation after scaling, y.</param>
    /// <returns>The path; segments before the first move start at the origin.</returns>
    public static Path ToPath(IReadOnlyList<SceneSegment> segments, double scale, double offsetX, double offsetY)
    {
        ArgumentNullException.ThrowIfNull(segments);
        var builder = new PathBuilder();
        System.Numerics.Vector2 Map(double x, double y) => new((float)((x * scale) + offsetX), (float)((y * scale) + offsetY));
        var started = false;
        foreach (var segment in segments)
        {
            // A drawing command without a current point starts at the origin (as an SVG path that does not begin with M)
            if (!started && segment.Kind is SceneSegmentKind.LineTo or SceneSegmentKind.CubicTo)
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
                case SceneSegmentKind.CubicTo:
                    builder.CubicBezierTo(Map(segment.X1, segment.Y1), Map(segment.X2, segment.Y2), Map(segment.X, segment.Y));
                    break;
                default:
                    builder.Close();
                    break;
            }
        }

        return builder.Build();
    }

    /// <summary>
    ///     Gets the bounds of path geometry.
    /// </summary>
    /// <param name="segments">The segments.</param>
    /// <returns>The bounds, or null for an empty path.</returns>
    private static SceneRect? PathBounds(IReadOnlyList<SceneSegment> segments)
    {
        // The control points of the shapes the scene draws lie on or inside their curves' bounding boxes (quarter
        // ellipses and corners), so the box of the points is the box of the geometry
        double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
        double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
        void Include(double x, double y)
        {
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
        }

        foreach (var segment in segments)
        {
            if (segment.Kind == SceneSegmentKind.Close)
            {
                continue;
            }

            Include(segment.X, segment.Y);
            if (segment.Kind == SceneSegmentKind.CubicTo)
            {
                Include(segment.X1, segment.Y1);
                Include(segment.X2, segment.Y2);
            }
        }

        return double.IsFinite(minX) ? new SceneRect(minX, minY, maxX - minX, maxY - minY) : null;
    }
}

/// <summary>
///     Builds the geometry of the shapes that diagrams draw: rectangles (with rounded corners), ellipses, polygons,
///     polylines, and lines.
/// </summary>
/// <remarks>
///     Every method returns a new list of segments in scene units. Ellipses and rounded corners are drawn with the
///     standard four-arc cubic approximation (the control distance 0.5523 of the radius), which differs from a true
///     ellipse by less than 0.03 % of the radius. The class is stateless and thread-safe.
/// </remarks>
internal static class SceneShapes
{
    /// <summary>The control distance of the cubic approximation of a quarter circle, as a fraction of the radius.</summary>
    private const double Kappa = 0.5522847498;

    /// <summary>
    ///     Builds a rectangle, with rounded corners when a radius is given (SVG's <c>rx</c> and <c>ry</c>).
    /// </summary>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The top edge.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="radius">The corner radius, clamped to half the smaller side; zero for square corners.</param>
    /// <returns>The closed outline.</returns>
    public static IReadOnlyList<SceneSegment> Rect(double x, double y, double width, double height, double radius = 0)
    {
        var r = Math.Max(0, Math.Min(radius, Math.Min(width, height) / 2));
        if (r <= 0)
        {
            return
            [
                SceneSegment.Move(x, y), SceneSegment.Line(x + width, y), SceneSegment.Line(x + width, y + height),
                SceneSegment.Line(x, y + height), SceneSegment.Close(),
            ];
        }

        var k = r * Kappa;
        var (right, bottom) = (x + width, y + height);
        return
        [
            SceneSegment.Move(x + r, y),
            SceneSegment.Line(right - r, y),
            SceneSegment.Cubic(right - r + k, y, right, y + r - k, right, y + r),
            SceneSegment.Line(right, bottom - r),
            SceneSegment.Cubic(right, bottom - r + k, right - r + k, bottom, right - r, bottom),
            SceneSegment.Line(x + r, bottom),
            SceneSegment.Cubic(x + r - k, bottom, x, bottom - r + k, x, bottom - r),
            SceneSegment.Line(x, y + r),
            SceneSegment.Cubic(x, y + r - k, x + r - k, y, x + r, y),
            SceneSegment.Close(),
        ];
    }

    /// <summary>
    ///     Builds an ellipse.
    /// </summary>
    /// <param name="cx">The center x.</param>
    /// <param name="cy">The center y.</param>
    /// <param name="rx">The horizontal radius.</param>
    /// <param name="ry">The vertical radius.</param>
    /// <returns>The closed outline.</returns>
    public static IReadOnlyList<SceneSegment> Ellipse(double cx, double cy, double rx, double ry)
    {
        var (kx, ky) = (rx * Kappa, ry * Kappa);
        return
        [
            SceneSegment.Move(cx + rx, cy),
            SceneSegment.Cubic(cx + rx, cy + ky, cx + kx, cy + ry, cx, cy + ry),
            SceneSegment.Cubic(cx - kx, cy + ry, cx - rx, cy + ky, cx - rx, cy),
            SceneSegment.Cubic(cx - rx, cy - ky, cx - kx, cy - ry, cx, cy - ry),
            SceneSegment.Cubic(cx + kx, cy - ry, cx + rx, cy - ky, cx + rx, cy),
            SceneSegment.Close(),
        ];
    }

    /// <summary>
    ///     Builds a closed polygon.
    /// </summary>
    /// <param name="points">The corners in order; at least one.</param>
    /// <returns>The closed outline.</returns>
    public static IReadOnlyList<SceneSegment> Polygon(params (double X, double Y)[] points)
    {
        ArgumentNullException.ThrowIfNull(points);
        var segments = Polyline(points).ToList();
        segments.Add(SceneSegment.Close());
        return segments;
    }

    /// <summary>
    ///     Builds an open polyline.
    /// </summary>
    /// <param name="points">The points in order; at least one.</param>
    /// <returns>The open path.</returns>
    public static IReadOnlyList<SceneSegment> Polyline(params (double X, double Y)[] points)
    {
        ArgumentNullException.ThrowIfNull(points);
        var segments = new List<SceneSegment>(points.Length);
        for (var i = 0; i < points.Length; i++)
        {
            segments.Add(i == 0 ? SceneSegment.Move(points[i].X, points[i].Y) : SceneSegment.Line(points[i].X, points[i].Y));
        }

        return segments;
    }

    /// <summary>
    ///     Builds a straight line.
    /// </summary>
    /// <param name="x1">The start x.</param>
    /// <param name="y1">The start y.</param>
    /// <param name="x2">The end x.</param>
    /// <param name="y2">The end y.</param>
    /// <returns>The open path.</returns>
    public static IReadOnlyList<SceneSegment> Line(double x1, double y1, double x2, double y2) =>
        [SceneSegment.Move(x1, y1), SceneSegment.Line(x2, y2)];
}
