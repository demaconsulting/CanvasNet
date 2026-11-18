// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;

namespace DemaConsulting.CanvasNet.PlantUml.Activity;

/// <summary>
///     The kind of a placed shape of an activity diagram.
/// </summary>
internal enum ActivityShapeKind
{
    /// <summary>A rectangle, with rounded corners when it has a radius.</summary>
    Rect,

    /// <summary>An ellipse.</summary>
    Ellipse,

    /// <summary>A closed outline (a diamond, an arrow head, a note).</summary>
    Polygon,

    /// <summary>An open outline (the title tab of a group).</summary>
    Path,

    /// <summary>A straight line.</summary>
    Line,

    /// <summary>One line of text.</summary>
    Text,

    /// <summary>Space that is kept free (it is not drawn).</summary>
    Empty,
}

/// <summary>
///     One shape of an activity diagram as PlantUML's activity engine draws it: an origin, a geometry relative to it,
///     its look, and how the compression of empty space treats it.
/// </summary>
/// <remarks>
///     <para>
///         The origin is the point PlantUML translates the shape to: the top left corner of a rectangle, an ellipse or
///         an empty space, the start of a line, the anchor of a polygon or path (its relative points are added to it),
///         and the start of a text's baseline. When the empty space is compressed, the origin moves and the shape keeps
///         its size, except that rectangles and lines are stretched between their transformed corners.
///     </para>
///     <para>
///         A shape is mutable only while the layout places it; the finished geometry is not changed again.
///     </para>
/// </remarks>
internal sealed class ActivityShape
{
    /// <summary>
    ///     Initializes a shape.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <param name="role">The role of the scene item it becomes.</param>
    public ActivityShape(ActivityShapeKind kind, string role)
    {
        Kind = kind;
        Role = role;
    }

    /// <summary>
    ///     Gets the kind.
    /// </summary>
    public ActivityShapeKind Kind { get; }

    /// <summary>
    ///     Gets the role of the scene item.
    /// </summary>
    public string Role { get; }

    /// <summary>
    ///     Gets or sets the origin's x.
    /// </summary>
    public double X { get; set; }

    /// <summary>
    ///     Gets or sets the origin's y.
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    ///     Gets or sets the width of a rectangle, ellipse, empty space or text.
    /// </summary>
    public double Width { get; set; }

    /// <summary>
    ///     Gets or sets the height of a rectangle, ellipse, empty space or text.
    /// </summary>
    public double Height { get; set; }

    /// <summary>
    ///     Gets or sets the end x of a line.
    /// </summary>
    public double X2 { get; set; }

    /// <summary>
    ///     Gets or sets the end y of a line.
    /// </summary>
    public double Y2 { get; set; }

    /// <summary>
    ///     Gets or sets the corner radius of a rectangle.
    /// </summary>
    public double Radius { get; set; }

    /// <summary>
    ///     Gets or sets the outline of a polygon or path, relative to the origin.
    /// </summary>
    public IReadOnlyList<SceneSegment> Segments { get; set; } = [];

    /// <summary>
    ///     Gets or sets the line of text, placed with its baseline start at the origin.
    /// </summary>
    public SceneText? Text { get; set; }

    /// <summary>
    ///     Gets or sets the fill, or null.
    /// </summary>
    public Rgba32? Fill { get; set; }

    /// <summary>
    ///     Gets or sets the stroke, or null.
    /// </summary>
    public SceneStroke? Stroke { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the compression on x ignores the shape.
    /// </summary>
    public bool IgnoreX { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the compression on y ignores the shape.
    /// </summary>
    public bool IgnoreY { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the extent of the drawing includes the shape (false for the title of
    ///     a group, which PlantUML draws as a special text, and for the space that only the compression sees).
    /// </summary>
    public bool CountsForLimits { get; set; } = true;

    /// <summary>
    ///     Gets or sets a value indicating whether the compression of empty space sees the shape.
    /// </summary>
    public bool CountsForSlots { get; set; } = true;

    /// <summary>
    ///     Gets or sets, for a text centered in a span (a swimlane title), the span, the width of the text block and the
    ///     offset of the line in it; the compression keeps the text centered in the compressed span.
    /// </summary>
    public (double Left, double Width, double BlockWidth, double LineOffset)? Center { get; set; }

    /// <summary>
    ///     Gets or sets the location of the construct the shape draws.
    /// </summary>
    public PlantUmlLocation Location { get; set; }

    /// <summary>
    ///     Creates a copy of the shape moved by an offset.
    /// </summary>
    /// <param name="dx">The horizontal offset.</param>
    /// <param name="dy">The vertical offset.</param>
    /// <returns>The moved copy.</returns>
    public ActivityShape Moved(double dx, double dy) => new(Kind, Role)
    {
        X = X + dx,
        Y = Y + dy,
        Width = Width,
        Height = Height,
        X2 = X2 + dx,
        Y2 = Y2 + dy,
        Radius = Radius,
        Segments = Segments,
        Text = Text,
        Fill = Fill,
        Stroke = Stroke,
        IgnoreX = IgnoreX,
        IgnoreY = IgnoreY,
        CountsForLimits = CountsForLimits,
        CountsForSlots = CountsForSlots,
        Center = Center,
        Location = Location,
    };
}

/// <summary>
///     The direction of a segment of an arrow (PlantUML's <c>Direction</c>).
/// </summary>
internal enum ArrowDirection
{
    /// <summary>No direction (a point).</summary>
    None,

    /// <summary>Toward the right.</summary>
    Right,

    /// <summary>Toward the left.</summary>
    Left,

    /// <summary>Downward.</summary>
    Down,

    /// <summary>Upward.</summary>
    Up,
}

/// <summary>
///     Where the label of an arrow is placed.
/// </summary>
internal enum SnakeLabelPlace
{
    /// <summary>Beside the first segment (PlantUML's default).</summary>
    Beside,

    /// <summary>Below the arrow, at its left (PlantUML's bottom alignment).</summary>
    Bottom,
}

/// <summary>
///     How an arrow may merge with the arrows that touch it (PlantUML's <c>MergeStrategy</c>).
/// </summary>
internal enum SnakeMerge
{
    /// <summary>Merges, and its path is simplified fully.</summary>
    Full,

    /// <summary>Merges, and its path is simplified without the last pattern.</summary>
    Limited,

    /// <summary>Never merges.</summary>
    None,
}

/// <summary>
///     An arrow of an activity diagram: a polyline with an optional head at its end, an optional head in the middle of
///     its first segment in one direction, and an optional label (PlantUML's <c>Snake</c>).
/// </summary>
internal sealed class Snake
{
    /// <summary>The points.</summary>
    private readonly List<(double X, double Y)> _points = [];

    /// <summary>
    ///     Initializes an arrow.
    /// </summary>
    /// <param name="link">The arrow's color and style.</param>
    /// <param name="end">The direction of the head at the end, or <see cref="ArrowDirection.None"/> for none.</param>
    public Snake(ActivityLink link, ArrowDirection end)
    {
        Link = link;
        End = end;
    }

    /// <summary>
    ///     Gets the color and style.
    /// </summary>
    public ActivityLink Link { get; }

    /// <summary>
    ///     Gets or sets the direction of the head at the end.
    /// </summary>
    public ArrowDirection End { get; set; }

    /// <summary>
    ///     Gets or sets how the arrow merges with the arrows that touch it.
    /// </summary>
    public SnakeMerge Merge { get; set; }

    /// <summary>
    ///     Gets or sets the direction of the first segment that gets a head in its middle.
    /// </summary>
    public ArrowDirection Emphasize { get; set; }

    /// <summary>
    ///     Gets or sets the label, or null.
    /// </summary>
    public TextBlock? Label { get; set; }

    /// <summary>
    ///     Gets or sets where the label is placed.
    /// </summary>
    public SnakeLabelPlace LabelPlace { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the compression on x ignores the heads.
    /// </summary>
    public bool IgnoreForCompression { get; set; }

    /// <summary>
    ///     Gets the points.
    /// </summary>
    public IReadOnlyList<(double X, double Y)> Points => _points;

    /// <summary>
    ///     Gets a value indicating whether no other arrow may end on this one's start (PlantUML's
    ///     <c>cannotBeTouched</c>): it does not merge fully, or it is one horizontal segment.
    /// </summary>
    public bool CannotBeTouched =>
        Merge != SnakeMerge.Full || (_points.Count == 2 && Math.Abs(_points[0].Y - _points[1].Y) < 1e-9);

    /// <summary>
    ///     Decides whether two points are the same (PlantUML's <c>Snake.same</c>).
    /// </summary>
    /// <param name="a">The first point.</param>
    /// <param name="b">The second point.</param>
    /// <returns>True when they are closer than a thousandth of a pixel.</returns>
    public static bool Same((double X, double Y) a, (double X, double Y) b) =>
        Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y))) < 0.001;

    /// <summary>
    ///     Merges another arrow into this one when one starts where the other ends (PlantUML's <c>Snake.merge</c>).
    /// </summary>
    /// <param name="other">The other arrow.</param>
    /// <returns>The merged arrow, or null when they do not merge.</returns>
    public Snake? TryMerge(Snake other)
    {
        var strategy = (SnakeMerge)Math.Max((int)Merge, (int)other.Merge);
        if (strategy == SnakeMerge.None || other.Label is { Lines.Count: > 0 })
        {
            return null;
        }

        if (Same(_points[^1], other._points[0]))
        {
            var result = new Snake(Link, other.End != ArrowDirection.None ? other.End : End)
            {
                Merge = strategy,
                Emphasize = Emphasize != ArrowDirection.None ? Emphasize : other.Emphasize,
                Label = Label,
                LabelPlace = LabelPlace,
                IgnoreForCompression = IgnoreForCompression,
            };
            foreach (var point in _points.Concat(other._points))
            {
                result.Add(point.X, point.Y);
            }

            WormSimplifier.Simplify(result._points, strategy == SnakeMerge.Full);
            return result;
        }

        return Same(_points[0], other._points[^1]) ? other.TryMerge(this) : null;
    }

    /// <summary>
    ///     Adds a point (a point equal to the previous one is skipped).
    /// </summary>
    /// <param name="x">The x.</param>
    /// <param name="y">The y.</param>
    /// <returns>This arrow.</returns>
    public Snake Add(double x, double y)
    {
        if (_points.Count > 0 && Math.Abs(_points[^1].X - x) < 1e-9 && Math.Abs(_points[^1].Y - y) < 1e-9)
        {
            return this;
        }

        _points.Add((x, y));
        return this;
    }

    /// <summary>
    ///     Gets the direction of a segment.
    /// </summary>
    /// <param name="from">The start.</param>
    /// <param name="to">The end.</param>
    /// <returns>The direction.</returns>
    public static ArrowDirection DirectionOf((double X, double Y) from, (double X, double Y) to)
    {
        if (to.X > from.X)
        {
            return ArrowDirection.Right;
        }

        if (to.X < from.X)
        {
            return ArrowDirection.Left;
        }

        if (to.Y > from.Y)
        {
            return ArrowDirection.Down;
        }

        return to.Y < from.Y ? ArrowDirection.Up : ArrowDirection.None;
    }

    /// <summary>
    ///     Gets the directions of the segments as PlantUML's code (<c>D</c>, <c>U</c>, <c>L</c>, <c>R</c>).
    /// </summary>
    /// <returns>The code.</returns>
    public string DirectionsCode()
    {
        var code = new char[Math.Max(0, _points.Count - 1)];
        for (var i = 0; i < code.Length; i++)
        {
            code[i] = DirectionOf(_points[i], _points[i + 1]) switch
            {
                ArrowDirection.Right => 'R',
                ArrowDirection.Left => 'L',
                ArrowDirection.Down => 'D',
                ArrowDirection.Up => 'U',
                _ => '.',
            };
        }

        return new string(code);
    }

    /// <summary>
    ///     Gets the top left corner of the label (PlantUML's <c>Snake.getTextBlockPosition</c>).
    /// </summary>
    /// <returns>The corner.</returns>
    public (double X, double Y) LabelPosition()
    {
        var label = Label!;
        var (p1, p2) = (_points[0], _points[1]);
        var x = Math.Max(p1.X, p2.X) + 4;
        var y = ((p1.Y + p2.Y) / 2) - (label.Height / 2);
        var code = DirectionsCode();
        if (LabelPlace == SnakeLabelPlace.Bottom)
        {
            return (_points.Min(p => p.X), _points.Max(p => p.Y));
        }

        if (code == "RD")
        {
            return (Math.Max(p1.X, p2.X), ((p1.Y + _points[2].Y) / 2) - (label.Height / 2));
        }

        if (code == "LD")
        {
            return (Math.Min(p1.X, p2.X), ((p1.Y + _points[2].Y) / 2) - (label.Height / 2));
        }

        return (x, y);
    }

    /// <summary>
    ///     Gets the largest x the arrow and its label reach.
    /// </summary>
    /// <returns>The x.</returns>
    public double MaxX()
    {
        var max = _points.Max(p => p.X);
        if (Label is { } label && label.Lines.Count > 0)
        {
            max = Math.Max(max, LabelPosition().X + label.Width);
        }

        return max;
    }
}
