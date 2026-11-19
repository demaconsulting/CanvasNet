// Portions derived from PlantUML (MIT); see third-party/README.md
using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;

namespace DemaConsulting.CanvasNet.PlantUml.Activity;

/// <summary>
///     The placed shapes of an activity diagram and the size of its drawing (its margins included).
/// </summary>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
/// <param name="Shapes">The shapes in paint order.</param>
internal sealed record ActivityGeometry(double Width, double Height, IReadOnlyList<ActivityShape> Shapes);

/// <summary>
///     Lays out an activity diagram as PlantUML's activity engine (the "ftile" composition of version 3) does.
/// </summary>
/// <remarks>
///     <para>
///         <b>Tiles.</b> Every instruction becomes a tile: an action is a padded box, a start a disc, an if a hexagon
///         with its branches side by side and a merge diamond, a while or repeat loop a hexagon or diamond with its body
///         and the arrows back, a fork its branches between two bars, a group a frame. A list stacks its tiles with 35
///         pixels (plus the height of an arrow's label) between them and draws the arrows. A tile places its children
///         and draws the arrows between them; nothing is solved globally, so the layout is a single pass over the tree.
///     </para>
///     <para>
///         <b>Compression.</b> The tiles keep generous space between them; as PlantUML does, the drawing is then
///         compressed: on each axis the bands that no shape occupies (lines do not count) are shrunk to 10 pixels, which
///         moves every shape and stretches lines and rectangles across the removed bands. The drawing is finally framed
///         at its extent with PlantUML's margins.
///     </para>
///     <para>
///         <b>Swimlanes.</b> With more than one swimlane, the tiles are drawn once per lane (only those in the lane),
///         each lane as wide as what it draws (and its title), side by side with dividers; the arrows between two lanes
///         are drawn by a last pass with the translation of both lanes.
///     </para>
///     <para>
///         <b>Limits.</b> The work of a layout is counted against <see cref="PlantUmlLimits.MaxLayoutWork"/>
///         (<see cref="PlantUmlDiagnosticCodes.LimitExceeded"/>) and the cancellation token is checked as it goes. An
///         instance lays out one diagram on one thread; <see cref="Run"/> is thread-safe.
///     </para>
/// </remarks>
internal sealed partial class ActivityLayout
{
    /// <summary>The half size of a diamond and the hexagon's point (PlantUML's <c>Hexagon.hexagonHalfSize</c>).</summary>
    public const double HexagonHalfSize = 12;

    /// <summary>The height of a fork bar.</summary>
    public const double BarHeight = 6;

    /// <summary>The margin of the document.</summary>
    private const double Margin = 10;

    /// <summary>The space between two stacked tiles before the compression.</summary>
    private const double AssemblySpace = 35;

    /// <summary>How many units of work pass between two cancellation checks.</summary>
    private const long CancellationStride = 256;

    /// <summary>The model.</summary>
    private readonly ActivityModel _model;

    /// <summary>The work done so far.</summary>
    private long _work;

    /// <summary>
    ///     Initializes a layout.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="context">The context of the call.</param>
    private ActivityLayout(ActivityModel model, DiagramRenderContext context)
    {
        _model = model;
        Context = context;
    }

    /// <summary>
    ///     Gets the context of the call.
    /// </summary>
    public DiagramRenderContext Context { get; }

    /// <summary>
    ///     Gets the theme.
    /// </summary>
    public SkinTheme Theme => _model.Theme;

    /// <summary>
    ///     Gets the swimlanes of the diagram.
    /// </summary>
    public IReadOnlyList<ActivityLane> Lanes => _model.Lanes;

    /// <summary>
    ///     Gets a value indicating whether the diagram is drawn in swimlanes (PlantUML draws them from two on).
    /// </summary>
    public bool HasLanes => _model.Lanes.Count > 1;

    /// <summary>
    ///     Gets the arrow color of the theme.
    /// </summary>
    public Rgba32 ArrowColor => Theme[SkinElement.ActivityArrow].Line ?? SkinTheme.LineColor;

    /// <summary>
    ///     Lays an activity diagram out.
    /// </summary>
    /// <param name="model">The model.</param>
    /// <param name="context">The context of the call.</param>
    /// <returns>The geometry.</returns>
    /// <exception cref="PlantUmlRenderException">Thrown when the layout needs more work than the limit allows.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the call is canceled.</exception>
    public static ActivityGeometry Run(ActivityModel model, DiagramRenderContext context)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(context);
        context.CancellationToken.ThrowIfCancellationRequested();
        return new ActivityLayout(model, context).Execute();
    }

    /// <summary>
    ///     Creates a stroke from a style, or null when the style has no line.
    /// </summary>
    /// <param name="style">The style.</param>
    /// <returns>The stroke.</returns>
    public static SceneStroke? StrokeOf(ElementStyle style) =>
        style.Line is { } line && style.LineWidth > 0 ? new SceneStroke(line, style.LineWidth) { Dash = style.Dash } : null;

    /// <summary>
    ///     Counts work and checks the limit and the cancellation.
    /// </summary>
    /// <param name="units">The units of work.</param>
    /// <exception cref="PlantUmlRenderException">Thrown when the work exceeds the limit.</exception>
    public void Tick(long units = 1)
    {
        var before = _work;
        _work += units;
        var limit = Context.Options.Limits.MaxLayoutWork;
        if (_work > limit)
        {
            throw new PlantUmlRenderException(
                PlantUmlDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"the layout needs more than {limit} units of work (PlantUmlLimits.MaxLayoutWork)"),
                _model.KeywordLocation,
                "simplify the diagram, or raise PlantUmlLimits.MaxLayoutWork when the source is trusted");
        }

        if (before / CancellationStride != _work / CancellationStride)
        {
            Context.CancellationToken.ThrowIfCancellationRequested();
        }
    }

    /// <summary>
    ///     Lays out a text in the style of an element.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="element">The element whose style is used.</param>
    /// <param name="location">The location for reports.</param>
    /// <returns>The block.</returns>
    public TextBlock Text(string text, SkinElement element, PlantUmlLocation location)
    {
        Tick(1 + (text.Length / 64));
        var style = Theme[element];
        var font = new SceneFont(style.FontFamily, style.FontSize, style.Bold, style.Italic);
        return Context.Text.Layout(text, font, style.FontColor, 0, Context.Sink, location);
    }

    /// <summary>
    ///     Lays out the label of an arrow, or returns null when there is none.
    /// </summary>
    /// <param name="link">The arrow.</param>
    /// <returns>The block, or null.</returns>
    public TextBlock? Label(ActivityLink link) => link.Label is { Length: > 0 } label ? Text(label, SkinElement.ActivityArrow, link.Location) : null;

    /// <summary>
    ///     Lays out a text of an arrow, or returns null when there is none.
    /// </summary>
    /// <param name="text">The text, or null.</param>
    /// <param name="location">The location.</param>
    /// <returns>The block, or null.</returns>
    public TextBlock? ArrowText(string? text, PlantUmlLocation location) =>
        text is { Length: > 0 } ? Text(text, SkinElement.ActivityArrow, location) : null;

    /// <summary>
    ///     Gets the color and style an arrow is drawn with: its own, or the theme's color.
    /// </summary>
    /// <param name="link">The arrow, or null.</param>
    /// <returns>The arrow with a color.</returns>
    public ActivityLink Rainbow(ActivityLink? link) =>
        link is { HasRainbow: true } ? link with { Color = link.Color ?? ArrowColor } : new ActivityLink(null, ArrowColor, ActivityLinkStyle.Normal, link?.Location ?? default);

    /// <summary>
    ///     Gets the extent of what a tile draws (PlantUML's <c>LimitFinder</c>), with its origin at zero.
    /// </summary>
    /// <param name="tile">The tile.</param>
    /// <returns>The extent, or null when it draws nothing.</returns>
    public SceneRect? LimitsOf(Tile tile)
    {
        var canvas = new TileCanvas(this);
        tile.Draw(canvas, 0, 0);
        return Limits(Flush(canvas));
    }

    /// <summary>
    ///     Lays the diagram out.
    /// </summary>
    /// <returns>The geometry.</returns>
    private ActivityGeometry Execute()
    {
        var root = BuildList(_model.Root, true);
        var shapes = HasLanes ? DrawLanes(root) : DrawSingle(root);
        Compress(shapes);

        // PlantUML moves the drawing on its extent with 5 pixels around, and adds the document margin
        var box = Limits(shapes) ?? new SceneRect(0, 0, 0, 0);
        var dx = -box.X + 5 + Margin;
        var dy = -box.Y + 5 + Margin;
        var placed = shapes.Select(s => s.Moved(dx, dy)).ToList();
        return new ActivityGeometry(box.Width + 15 + (2 * Margin), box.Height + 15 + (2 * Margin), placed);
    }

    /// <summary>
    ///     Draws a diagram without swimlanes.
    /// </summary>
    /// <param name="root">The tile of the diagram.</param>
    /// <returns>The shapes, arrows last.</returns>
    private List<ActivityShape> DrawSingle(Tile root)
    {
        var canvas = new TileCanvas(this);
        root.Draw(canvas, 0, 0);
        return Flush(canvas);
    }

    /// <summary>
    ///     Converts the recorded arrows of a canvas into shapes after its other shapes (PlantUML draws the arrows when
    ///     the drawing is flushed).
    /// </summary>
    /// <param name="canvas">The canvas.</param>
    /// <returns>The shapes.</returns>
    private List<ActivityShape> Flush(TileCanvas canvas)
    {
        var shapes = new List<ActivityShape>(canvas.Shapes);

        // As PlantUML's UGraphicForSnake does: an arrow that starts where another ends is merged into it, and an arrow
        // that ends where another starts loses its head
        var pending = new List<Snake>();
        foreach (var (snake, _) in canvas.Snakes)
        {
            Tick();
            var merged = false;
            for (var i = 0; i < pending.Count && !merged; i++)
            {
                if (pending[i].TryMerge(snake) is { } result)
                {
                    pending[i] = result;
                    merged = true;
                }
            }

            if (!merged)
            {
                pending.Add(snake);
            }
        }

        foreach (var snake in pending)
        {
            if (pending.Exists(other => !other.CannotBeTouched && Snake.Same(snake.Points[^1], other.Points[0])))
            {
                snake.End = ArrowDirection.None;
            }

            AddSnake(shapes, snake);
        }

        return shapes;
    }

    /// <summary>
    ///     Converts an arrow into shapes: its segments, its heads, and its label.
    /// </summary>
    /// <param name="shapes">The shapes.</param>
    /// <param name="snake">The arrow.</param>
    private void AddSnake(List<ActivityShape> shapes, Snake snake)
    {
        Tick(snake.Points.Count);
        var link = snake.Link;
        var color = link.Color ?? ArrowColor;
        if (link.Style != ActivityLinkStyle.Hidden)
        {
            var stroke = link.Style switch
            {
                ActivityLinkStyle.Dashed => new SceneStroke(color, 1.5) { Dash = [7, 7] },
                ActivityLinkStyle.Dotted => new SceneStroke(color, 1.5) { Dash = [1, 3] },
                ActivityLinkStyle.Bold => new SceneStroke(color, 2),
                _ => new SceneStroke(color, 1),
            };
            var emphasized = false;
            for (var i = 0; i + 1 < snake.Points.Count; i++)
            {
                var (p1, p2) = (snake.Points[i], snake.Points[i + 1]);
                shapes.Add(new ActivityShape(ActivityShapeKind.Line, "arrow") { X = p1.X, Y = p1.Y, X2 = p2.X, Y2 = p2.Y, Stroke = stroke, Location = link.Location });
                if (!emphasized && snake.Emphasize != ArrowDirection.None && Snake.DirectionOf(p1, p2) == snake.Emphasize)
                {
                    emphasized = true;
                    shapes.Add(Head(snake.Emphasize, (p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2, color, false, link.Location));
                }
            }

            if (snake.End != ArrowDirection.None)
            {
                var end = snake.Points[^1];
                shapes.Add(Head(snake.End, end.X, end.Y, color, snake.IgnoreForCompression, link.Location));
            }
        }

        if (snake.Label is { Lines.Count: > 0 } label)
        {
            var (x, y) = snake.LabelPosition();
            foreach (var line in label.Draw(x, y, TextAlign.Left, "arrow-text", link.Location))
            {
                shapes.Add(new ActivityShape(ActivityShapeKind.Text, "arrow-text")
                {
                    X = line.X,
                    Y = line.Baseline,
                    Width = line.Box.Width,
                    Height = line.Box.Height,
                    Text = line,
                    Location = link.Location,
                });
            }
        }
    }

    /// <summary>
    ///     Creates an arrow head with its tip at a point (PlantUML's <c>ArrowsRegular</c>).
    /// </summary>
    /// <param name="direction">The direction it points to.</param>
    /// <param name="x">The x of the tip.</param>
    /// <param name="y">The y of the tip.</param>
    /// <param name="color">The color.</param>
    /// <param name="ignoreX">Whether the compression on x ignores it.</param>
    /// <param name="location">The location.</param>
    /// <returns>The head.</returns>
    internal static ActivityShape Head(ArrowDirection direction, double x, double y, Rgba32 color, bool ignoreX, PlantUmlLocation location)
    {
        (double X, double Y)[] points = direction switch
        {
            ArrowDirection.Up => [(-4, 10), (0, 0), (4, 10), (0, 6)],
            ArrowDirection.Right => [(-10, -4), (0, 0), (-10, 4), (-6, 0)],
            ArrowDirection.Left => [(10, -4), (0, 0), (10, 4), (6, 0)],
            _ => [(-4, -10), (0, 0), (4, -10), (0, -6)],
        };
        return new ActivityShape(ActivityShapeKind.Polygon, "arrow-head")
        {
            X = x,
            Y = y,
            Segments = SceneShapes.Polygon(points),
            Fill = color,
            Stroke = new SceneStroke(color, 1),
            IgnoreX = ignoreX,
            Location = location,
        };
    }

    /// <summary>
    ///     Gets the extent of shapes by PlantUML's <c>LimitFinder</c> rules.
    /// </summary>
    /// <param name="shapes">The shapes.</param>
    /// <returns>The extent, or null when no shape counts.</returns>
    public static SceneRect? Limits(IEnumerable<ActivityShape> shapes)
    {
        double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
        double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
        void Include(double x, double y)
        {
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
        }

        foreach (var shape in shapes.Where(s => s.CountsForLimits))
        {
            switch (shape.Kind)
            {
                case ActivityShapeKind.Rect:
                    Include(shape.X - 1, shape.Y - 1);
                    Include(shape.X + shape.Width - 1, shape.Y + shape.Height - 1);
                    break;
                case ActivityShapeKind.Ellipse:
                    Include(shape.X, shape.Y);
                    Include(shape.X + shape.Width - 1, shape.Y + shape.Height - 1);
                    break;
                case ActivityShapeKind.Polygon or ActivityShapeKind.Path:
                    var hack = shape.Kind == ActivityShapeKind.Polygon && shape.Role != "note" ? 10 : 0;
                    var (x0, y0, x1, y1) = Extent(shape.Segments);
                    Include(shape.X + x0 - hack, shape.Y + y0);
                    Include(shape.X + x1 + hack, shape.Y + y1);
                    break;
                case ActivityShapeKind.Line:
                    Include(shape.X, shape.Y);
                    Include(shape.X2, shape.Y2);
                    break;
                case ActivityShapeKind.Text:
                    Include(shape.X, shape.Y - shape.Height + 1.5);
                    Include(shape.X + shape.Width, shape.Y + 1.5);
                    break;
                default:
                    Include(shape.X, shape.Y);
                    Include(shape.X + shape.Width, shape.Y + shape.Height);
                    break;
            }
        }

        return double.IsFinite(minX) ? new SceneRect(minX, minY, maxX - minX, maxY - minY) : null;
    }

    /// <summary>
    ///     Gets the extent of relative segments.
    /// </summary>
    /// <param name="segments">The segments.</param>
    /// <returns>The smallest and largest x and y.</returns>
    private static (double MinX, double MinY, double MaxX, double MaxY) Extent(IReadOnlyList<SceneSegment> segments)
    {
        double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
        double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
        foreach (var segment in segments.Where(s => s.Kind != SceneSegmentKind.Close))
        {
            minX = Math.Min(minX, segment.X);
            minY = Math.Min(minY, segment.Y);
            maxX = Math.Max(maxX, segment.X);
            maxY = Math.Max(maxY, segment.Y);
        }

        return double.IsFinite(minX) ? (minX, minY, maxX, maxY) : (0, 0, 0, 0);
    }

    /// <summary>
    ///     Compresses the empty bands of a drawing on both axes (PlantUML's <c>CompressionXorYBuilder</c>).
    /// </summary>
    /// <param name="shapes">The shapes, changed in place.</param>
    private void Compress(List<ActivityShape> shapes)
    {
        var onX = Gaps(shapes, true);
        var onY = Gaps(shapes, false);
        double Fx(double v) => v - Removed(onX, v);
        double Fy(double v) => v - Removed(onY, v);
        foreach (var shape in shapes)
        {
            Tick();
            switch (shape.Kind)
            {
                case ActivityShapeKind.Rect or ActivityShapeKind.Empty:
                    var (right, bottom) = (shape.X + shape.Width, shape.Y + shape.Height);
                    shape.X = Fx(shape.X);
                    shape.Y = Fy(shape.Y);
                    shape.Width = Fx(right) - shape.X;
                    shape.Height = Fy(bottom) - shape.Y;
                    break;
                case ActivityShapeKind.Line:
                    (shape.X, shape.Y, shape.X2, shape.Y2) = (Fx(shape.X), Fy(shape.Y), Fx(shape.X2), Fy(shape.Y2));
                    break;
                case ActivityShapeKind.Text when shape.Center is { } center:
                    var left = Fx(center.Left);
                    shape.X = left + ((Fx(center.Left + center.Width) - left - center.BlockWidth) / 2) + center.LineOffset;
                    shape.Y = Fy(shape.Y);
                    break;
                default:
                    shape.X = Fx(shape.X);
                    shape.Y = Fy(shape.Y);
                    break;
            }
        }
    }

    /// <summary>
    ///     Finds the bands of an axis that no shape occupies and that can shrink: the gaps between the occupied
    ///     extents, each reduced by 5 pixels on both sides (PlantUML's <c>SlotFinder</c> and <c>SlotSet</c>).
    /// </summary>
    /// <param name="shapes">The shapes.</param>
    /// <param name="horizontal">Whether the axis is x.</param>
    /// <returns>The removable bands, sorted.</returns>
    private static List<(double Start, double End)> Gaps(List<ActivityShape> shapes, bool horizontal)
    {
        var slots = new List<(double Start, double End)>();
        void Occupy(double start, double end)
        {
            if (end > start)
            {
                slots.Add((start, end));
            }
        }

        foreach (var shape in shapes.Where(s => s.CountsForSlots))
        {
            var origin = horizontal ? shape.X : shape.Y;
            var size = horizontal ? shape.Width : shape.Height;
            var ignored = horizontal ? shape.IgnoreX : shape.IgnoreY;
            switch (shape.Kind)
            {
                case ActivityShapeKind.Rect when ignored:
                    // An ignored rectangle keeps only its two edges
                    Occupy(origin, origin + 2);
                    Occupy(origin + size - 2, origin + size);
                    break;
                case ActivityShapeKind.Rect or ActivityShapeKind.Ellipse or ActivityShapeKind.Empty:
                    Occupy(origin, origin + size);
                    break;
                case ActivityShapeKind.Polygon or ActivityShapeKind.Path when !ignored:
                    var (x0, y0, x1, y1) = Extent(shape.Segments);
                    Occupy(origin + (horizontal ? x0 : y0), origin + (horizontal ? x1 : y1));
                    break;
                case ActivityShapeKind.Text when !ignored:
                    if (horizontal)
                    {
                        Occupy(shape.X, shape.X + shape.Width);
                    }
                    else
                    {
                        Occupy(shape.Y - shape.Height + 1.5, shape.Y + 1.5);
                    }

                    break;
            }
        }

        // Merge the occupied extents (touching ones too), then keep the gaps wider than 10 pixels, minus 5 on each side
        slots.Sort((a, b) => a.Start.CompareTo(b.Start));
        var gaps = new List<(double Start, double End)>();
        if (slots.Count == 0)
        {
            return gaps;
        }

        var end = slots[0].End;
        for (var i = 1; i < slots.Count; i++)
        {
            if (slots[i].Start > end)
            {
                if (slots[i].Start - end > 10)
                {
                    gaps.Add((end + 5, slots[i].Start - 5));
                }

                end = slots[i].End;
            }
            else
            {
                end = Math.Max(end, slots[i].End);
            }
        }

        return gaps;
    }

    /// <summary>
    ///     Gets how much the compression removes before a coordinate (PlantUML's <c>CompressionTransform</c>).
    /// </summary>
    /// <param name="gaps">The removed bands.</param>
    /// <param name="value">The coordinate.</param>
    /// <returns>The removed length.</returns>
    private static double Removed(List<(double Start, double End)> gaps, double value)
    {
        double removed = 0;
        foreach (var (start, end) in gaps)
        {
            if (start > value)
            {
                break;
            }

            removed += value > end ? end - start : value - start;
        }

        return removed;
    }
}
