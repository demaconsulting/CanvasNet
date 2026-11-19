// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;

namespace DemaConsulting.CanvasNet.PlantUml.Activity;

/// <summary>
///     A note shape with a folded corner (PlantUML's <c>Opale</c>), optionally with a pointer to its instruction.
/// </summary>
internal sealed class NoteShape
{
    /// <summary>The size of the folded corner.</summary>
    private const double CornerSize = 10;

    /// <summary>The space left of the text.</summary>
    private const double MarginX1 = 6;

    /// <summary>The space right of the text.</summary>
    private const double MarginX2 = 15;

    /// <summary>The space above and below the text.</summary>
    private const double MarginY = 5;

    /// <summary>The half height of the pointer where it leaves the note.</summary>
    private const double Delta = 4;

    /// <summary>The note.</summary>
    private readonly ActivityNote _note;

    /// <summary>The laid-out text.</summary>
    private readonly TextBlock _text;

    /// <summary>The style.</summary>
    private readonly ElementStyle _style;

    /// <summary>
    ///     Initializes a note shape.
    /// </summary>
    /// <param name="layout">The layout.</param>
    /// <param name="note">The note.</param>
    public NoteShape(ActivityLayout layout, ActivityNote note)
    {
        _note = note;
        _style = layout.Theme[SkinElement.Note];
        _text = layout.Text(note.Text, SkinElement.Note, note.Location);
    }

    /// <summary>
    ///     Gets the note.
    /// </summary>
    public ActivityNote Note => _note;

    /// <summary>
    ///     Gets the width.
    /// </summary>
    public double Width => _text.Width + MarginX1 + MarginX2;

    /// <summary>
    ///     Gets the height.
    /// </summary>
    public double Height => _text.Height + (2 * MarginY);

    /// <summary>
    ///     Draws the note with its top left corner at a point.
    /// </summary>
    /// <param name="canvas">The canvas.</param>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    /// <param name="pointer">The tip of the pointer relative to the corner, on the left or right side, or null for none.</param>
    public void Draw(TileCanvas canvas, double x, double y, (double X, double Y)? pointer)
    {
        var (width, height) = (Width, Height);
        (double X, double Y)[] outline;
        if (pointer is not { } tip)
        {
            outline = [(0, 0), (0, height), (width, height), (width, CornerSize), (width - CornerSize, 0), (0, 0)];
        }
        else if (tip.X < 0)
        {
            var y1 = Math.Clamp((height / 2) - Delta, 0, Math.Max(0, height - (2 * Delta)));
            outline = [(0, 0), (0, y1), tip, (0, y1 + (2 * Delta)), (0, height), (width, height), (width, CornerSize), (width - CornerSize, 0), (0, 0)];
        }
        else
        {
            var y1 = Math.Clamp((height / 2) - Delta, CornerSize, Math.Max(CornerSize, height - (2 * Delta)));
            outline = [(0, 0), (0, height), (width, height), (width, y1 + (2 * Delta)), tip, (width, y1), (width, CornerSize), (width - CornerSize, 0), (0, 0)];
        }

        var fill = _note.Fill ?? _style.Fill;
        var stroke = ActivityLayout.StrokeOf(_style);
        canvas.Polygon("note", x, y, outline, fill, stroke, _note.Location);
        canvas.Polygon("note", x, y, [(width - CornerSize, 0), (width - CornerSize, CornerSize), (width, CornerSize), (width - CornerSize, 0)], fill, stroke, _note.Location);
        canvas.Text(_text, x + MarginX1, y + MarginY, TextAlign.Left, "note-text", _note.Location);
    }
}

/// <summary>
///     A tile with one note beside it (PlantUML's <c>FtileWithNoteOpale</c>): the note is 20 pixels away, centered
///     vertically, with a pointer to the tile unless it is a floating note.
/// </summary>
/// <param name="inner">The tile.</param>
/// <param name="note">The note.</param>
/// <param name="withLink">Whether the note points to the tile.</param>
internal sealed class NoteTile(Tile inner, ActivityNote note, bool withLink) : DecoratedTile(inner)
{
    /// <summary>The space between the note and the tile.</summary>
    private const double SuppSpace = 20;

    /// <summary>The note shape.</summary>
    private readonly NoteShape _shape = new(inner.Layout, note);

    /// <inheritdoc/>
    protected override IReadOnlySet<ActivityLane> ComputeLanes() =>
        note.Lane is { } lane ? new HashSet<ActivityLane>(Inner.Lanes) { lane } : Inner.Lanes;

    /// <summary>
    ///     Gets the size of the tile with its note.
    /// </summary>
    private (double Width, double Height) Total =>
        (Inner.Geometry.Width + _shape.Width + SuppSpace, Math.Max(_shape.Height, Inner.Geometry.Height));

    /// <summary>
    ///     Gets the offset of the inner tile.
    /// </summary>
    private (double X, double Y) InnerOffset =>
        (note.Position == ActivityNotePosition.Left ? _shape.Width + SuppSpace : 0, (Total.Height - Inner.Geometry.Height) / 2);

    /// <inheritdoc/>
    public override void Draw(TileCanvas canvas, double x, double y)
    {
        var (width, height) = Total;
        var noteY = y + ((height - _shape.Height) / 2);
        var link = withLink && !note.Floating;
        if (canvas.Mode != CanvasMode.Lane || note.Lane is null || canvas.Lane == note.Lane)
        {
            if (note.Position == ActivityNotePosition.Left)
            {
                _shape.Draw(canvas, x, noteY, link ? (_shape.Width + SuppSpace, _shape.Height / 2) : null);
            }
            else
            {
                _shape.Draw(canvas, x + width - _shape.Width, noteY, link ? (-SuppSpace, _shape.Height / 2) : null);
            }
        }

        canvas.Tile(Inner, x + InnerOffset.X, y + InnerOffset.Y);
    }

    /// <inheritdoc/>
    protected override TileGeometry ComputeGeometry()
    {
        var (width, height) = Total;
        var (dx, dy) = InnerOffset;
        var geometry = Inner.Geometry;
        return new TileGeometry(width, height, geometry.Left + dx, geometry.InY + dy, geometry.OutY + dy);
    }
}

/// <summary>
///     A tile with several notes beside it (PlantUML's <c>FtileWithNotes</c>): the notes of each side are stacked with
///     a margin of 10 pixels, without pointers.
/// </summary>
/// <param name="inner">The tile.</param>
/// <param name="notes">The notes.</param>
internal sealed class NotesTile(Tile inner, IReadOnlyList<ActivityNote> notes) : DecoratedTile(inner)
{
    /// <summary>The margin around each note.</summary>
    private const double Margin = 10;

    /// <summary>The notes on the left.</summary>
    private readonly List<NoteShape> _left = [.. notes.Where(n => n.Position == ActivityNotePosition.Left).Select(n => new NoteShape(inner.Layout, n))];

    /// <summary>The notes on the right.</summary>
    private readonly List<NoteShape> _right = [.. notes.Where(n => n.Position == ActivityNotePosition.Right).Select(n => new NoteShape(inner.Layout, n))];

    /// <summary>
    ///     Gets the size of a stack of notes with their margins.
    /// </summary>
    /// <param name="stack">The notes.</param>
    /// <returns>The size.</returns>
    private static (double Width, double Height) SizeOf(List<NoteShape> stack) =>
        stack.Count == 0 ? (0, 0) : (stack.Max(n => n.Width) + (2 * Margin), stack.Sum(n => n.Height + (2 * Margin)));

    /// <summary>
    ///     Gets the size of the tile with its notes.
    /// </summary>
    private (double Width, double Height) Total
    {
        get
        {
            var (left, right) = (SizeOf(_left), SizeOf(_right));
            return (Inner.Geometry.Width + left.Width + right.Width, Math.Max(Math.Max(left.Height, right.Height), Inner.Geometry.Height));
        }
    }

    /// <inheritdoc/>
    public override void Draw(TileCanvas canvas, double x, double y)
    {
        var (width, height) = Total;
        var left = SizeOf(_left);
        var right = SizeOf(_right);
        DrawStack(canvas, _left, x, y + ((height - left.Height) / 2), left.Width);
        DrawStack(canvas, _right, x + width - right.Width, y + ((height - right.Height) / 2), right.Width);
        canvas.Tile(Inner, x + left.Width, y + ((height - Inner.Geometry.Height) / 2));
    }

    /// <summary>
    ///     Draws a stack of notes, each centered in the width of the stack.
    /// </summary>
    /// <param name="canvas">The canvas.</param>
    /// <param name="stack">The notes.</param>
    /// <param name="x">The left of the stack.</param>
    /// <param name="y">The top of the stack.</param>
    /// <param name="width">The width of the stack.</param>
    private static void DrawStack(TileCanvas canvas, List<NoteShape> stack, double x, double y, double width)
    {
        foreach (var note in stack)
        {
            note.Draw(canvas, x + ((width - note.Width - (2 * Margin)) / 2) + Margin, y + Margin, null);
            y += note.Height + (2 * Margin);
        }
    }

    /// <inheritdoc/>
    protected override TileGeometry ComputeGeometry()
    {
        var (width, height) = Total;
        var dx = SizeOf(_left).Width;
        var dy = (height - Inner.Geometry.Height) / 2;
        var geometry = Inner.Geometry;
        return new TileGeometry(width, height, geometry.Left + dx, geometry.InY + dy, geometry.OutY + dy);
    }
}

/// <summary>
///     A note that is an instruction of its own (PlantUML's <c>FtileNoteAlone</c>): a note written before the first
///     instruction of a list.
/// </summary>
/// <param name="layout">The layout.</param>
/// <param name="note">The note.</param>
/// <param name="lane">The swimlane.</param>
internal sealed class NoteAloneTile(ActivityLayout layout, ActivityNote note, ActivityLane? lane) : Tile(layout)
{
    /// <summary>The note shape.</summary>
    private readonly NoteShape _shape = new(layout, note);

    /// <inheritdoc/>
    public override ActivityLane? LaneIn => lane;

    /// <inheritdoc/>
    public override void Draw(TileCanvas canvas, double x, double y) => _shape.Draw(canvas, x, y, null);

    /// <inheritdoc/>
    protected override TileGeometry ComputeGeometry() => new(_shape.Width, _shape.Height, _shape.Width / 2, 0, _shape.Height);
}

/// <summary>
///     A partition or group: a frame with a title tab around a tile (PlantUML's <c>FtileGroup</c> with the frame
///     symbol).
/// </summary>
internal sealed class GroupTile : DecoratedTile
{
    /// <summary>The space below the inner tile.</summary>
    private const double BottomSpace = 20;

    /// <summary>The group.</summary>
    private readonly ActivityGroup _group;

    /// <summary>The laid-out title.</summary>
    private readonly TextBlock _title;

    /// <summary>The geometry of the inner tile, widened to what it draws.</summary>
    private TileGeometry? _innerGeometry;

    /// <summary>
    ///     Initializes a group tile.
    /// </summary>
    /// <param name="inner">The tile of the instructions.</param>
    /// <param name="group">The group.</param>
    public GroupTile(Tile inner, ActivityGroup group)
        : base(new MarginTile(inner, 10, 10))
    {
        _group = group;
        _title = Layout.Text(group.Title, SkinElement.Partition, group.Location);
    }

    /// <summary>
    ///     Gets the height above the inner tile: the title and its spacing.
    /// </summary>
    private double TitleHeight => Math.Max(25, _title.Height + 20);

    /// <inheritdoc/>
    public override ActivityLink OutLink => ActivityLink.None;

    /// <summary>
    ///     Gets the geometry of the inner tile, widened when it draws beyond its width (a label or a note).
    /// </summary>
    private TileGeometry InnerGeometry
    {
        get
        {
            if (_innerGeometry is { } cached)
            {
                return cached;
            }

            var orig = Inner.Geometry;
            var limits = Layout.LimitsOf(Inner);
            var missing = limits is { } box ? box.Right - orig.Width : 0;
            _innerGeometry = missing > 0 ? orig.AddDim(missing + 5, 0) : orig;
            return _innerGeometry.Value;
        }
    }

    /// <summary>
    ///     Gets the width the title needs beyond the inner tile.
    /// </summary>
    private double SuppWidth => Math.Max(InnerGeometry.Width, _title.Width + 20) - InnerGeometry.Width;

    /// <inheritdoc/>
    public override void Draw(TileCanvas canvas, double x, double y)
    {
        var style = Layout.Theme[SkinElement.Partition];
        var geometry = Geometry;
        var stroke = ActivityLayout.StrokeOf(style);
        var frame = canvas.Rect("partition", x, y, geometry.Width, geometry.Height, style.RoundCorner / 2, _group.Fill ?? style.Fill, stroke, _group.Location);
        frame.IgnoreX = true;
        frame.IgnoreY = true;

        // The title tab
        var textWidth = _title.Width == 0 ? geometry.Width / 3 : _title.Width + 10;
        var corner = _title.Width == 0 ? 7 : 10;
        var textHeight = _title.Width == 0 ? 12 : _title.Height + 3;
        var tab = canvas.Path("partition", x, y, [(textWidth, 0), (textWidth, textHeight - corner), (textWidth - corner, textHeight), (0, textHeight)], stroke, _group.Location);
        tab.IgnoreX = true;

        // PlantUML draws a title that leaves room as a special text, which the extent of the drawing does not see
        var special = geometry.Width - _title.Width >= 25;
        var before = canvas.Shapes.Count;
        canvas.Text(_title, x + 3, y + 1, TextAlign.Left, "partition-text", _group.Location);
        if (special)
        {
            for (var i = before; i < canvas.Shapes.Count; i++)
            {
                canvas.Shapes[i].CountsForLimits = false;
                canvas.Shapes[i].CountsForSlots = false;
            }

            canvas.Add(new ActivityShape(ActivityShapeKind.Empty, "empty")
            {
                X = x + 3 + _title.Width,
                Y = y + 1,
                Width = 1,
                Height = 1,
                CountsForLimits = false,
            });
        }

        canvas.Tile(Inner, x + (SuppWidth / 2), y + TitleHeight);
    }

    /// <inheritdoc/>
    protected override TileGeometry ComputeGeometry()
    {
        var orig = InnerGeometry;
        var supp = SuppWidth;
        var width = orig.Width + supp;
        var height = orig.Height + TitleHeight + BottomSpace;
        return new TileGeometry(width, height, orig.Left + (supp / 2), orig.InY + TitleHeight, orig.OutY + TitleHeight);
    }
}
