// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;

namespace DemaConsulting.CanvasNet.PlantUml.Activity;

/// <content>
///     The loops: while (PlantUML's <c>FtileWhile</c>) and repeat (<c>FtileRepeat</c>).
/// </content>
internal sealed partial class ActivityLayout
{
    /// <summary>
    ///     Builds the tile of a while loop.
    /// </summary>
    /// <param name="loop">The loop.</param>
    /// <returns>The tile.</returns>
    private Tile BuildWhile(ActivityWhile loop)
    {
        var body = BuildList(loop.Body);
        var test = loop.Test.Trim().Length == 0 ? TextBlock.Empty : Text(loop.Test, SkinElement.ActivityDiamond, loop.Location);
        var diamond = new HexagonTile(this, test, loop.Color ?? DiamondFill, loop.Lane)
        {
            North = ArrowText(loop.Yes, loop.Location),
            West = Label(loop.OutLink),
            Location = loop.Location,
        };
        var special = loop.SpecialOut is { } stop ? Build(stop, false) : null;
        var backward = loop.Backward is { } text ? new BoxTile(this, text, null, loop.Lane, ActivityLink.None, loop.Location) : null;
        Tile result = WhileTile.Create(this, loop, body, diamond, special, backward);
        if (loop.Notes.Count > 0)
        {
            result = NotesOf(result, loop.Notes, false);
        }

        return loop.Killed || special is not null ? new KilledTile(result) : result;
    }

    /// <summary>
    ///     Builds the tile of a repeat loop.
    /// </summary>
    /// <param name="repeat">The loop.</param>
    /// <param name="isLastOfRoot">Whether the loop is the last instruction of the diagram.</param>
    /// <returns>The tile.</returns>
    private Tile BuildRepeat(ActivityRepeat repeat, bool isLastOfRoot)
    {
        var body = new DecoratedTile(BuildList(repeat.Body), outLink: repeat.EndRepeatLink);
        Tile? backward = null;
        if (repeat.Backward is { } text)
        {
            backward = new BoxTile(this, text, null, repeat.BackwardLane, ActivityLink.None, repeat.Location);
            if (repeat.BackwardNotes.Count > 0)
            {
                backward = NotesOf(backward, repeat.BackwardNotes, true);
            }
        }

        var fill = repeat.Color ?? DiamondFill;
        Tile entry = repeat.StartLabel is { } label
            ? new BoxTile(this, label, repeat.Color ?? Theme[SkinElement.Activity].Fill, repeat.Lane, ActivityLink.None, repeat.Location)
            : new DiamondTile(this, fill, repeat.Lane) { Location = repeat.Location };
        var test = repeat.Test is not { } value || value.Trim().Length == 0 ? TextBlock.Empty : Text(value, SkinElement.ActivityDiamond, repeat.Location);
        Tile exit;
        if (isLastOfRoot && repeat.Test is null)
        {
            exit = new EmptyTile(this, 0, 0, null);
        }
        else
        {
            var yes = ArrowText(repeat.Yes, repeat.Location);
            var exitsOnLeft = backward?.LaneIn is { } backLane && repeat.LaneOut is { } outLane && backLane.Index < outLane.Index;
            exit = new HexagonTile(this, test, fill, repeat.LaneOut)
            {
                West = exitsOnLeft ? yes : null,
                East = exitsOnLeft ? null : yes,
                South = ArrowText(repeat.Out, repeat.Location),
                Location = repeat.Location,
            };
        }

        Tile result = RepeatTile.Create(this, repeat, entry, body, exit, backward);
        return repeat.Killed ? new KilledTile(result) : result;
    }
}

/// <summary>
///     A while loop (PlantUML's <c>FtileWhile</c>): the hexagon above the body, the arrow back on the right with an
///     upward head, and the exit on the left.
/// </summary>
internal sealed class WhileTile : Tile
{
    /// <summary>The body.</summary>
    private readonly Tile _body;

    /// <summary>The hexagon.</summary>
    private readonly HexagonTile _diamond;

    /// <summary>The stop or end on the exit, or null.</summary>
    private readonly Tile? _special;

    /// <summary>The backward action, or null.</summary>
    private readonly Tile? _backward;

    /// <summary>The loop.</summary>
    private readonly ActivityWhile _loop;

    /// <summary>The label of the arrow back, or null.</summary>
    private readonly TextBlock? _back1;

    /// <summary>
    ///     Initializes the tile.
    /// </summary>
    /// <param name="layout">The layout.</param>
    /// <param name="loop">The loop.</param>
    /// <param name="body">The body.</param>
    /// <param name="diamond">The hexagon.</param>
    /// <param name="special">The stop on the exit.</param>
    /// <param name="backward">The backward action.</param>
    private WhileTile(ActivityLayout layout, ActivityWhile loop, Tile body, HexagonTile diamond, Tile? special, Tile? backward)
        : base(layout)
    {
        _loop = loop;
        _body = body;
        _diamond = diamond;
        _special = special;
        _backward = backward;
        _back1 = layout.Label(loop.Incoming1);
    }

    /// <inheritdoc/>
    public override ActivityLane? LaneIn => _loop.Lane;

    /// <inheritdoc/>
    protected override IReadOnlySet<ActivityLane> ComputeLanes()
    {
        var lanes = new HashSet<ActivityLane>(_body.Lanes);
        if (_loop.Lane is { } lane)
        {
            lanes.Add(lane);
        }

        return lanes;
    }

    /// <summary>
    ///     Gets the height of the label of the arrow back.
    /// </summary>
    private double BackLabelHeight => _back1?.Height ?? 0;

    /// <summary>
    ///     Gets the width of the stop on the exit.
    /// </summary>
    private double SpecialWidth => _special?.Geometry.Width ?? 0;

    /// <summary>
    ///     Gets the offset of the body.
    /// </summary>
    private (double X, double Y) BodyOffset
    {
        get
        {
            var total = Geometry;
            var d = _diamond.Geometry;
            var y = d.Height + ((total.Height - d.Height - _body.Geometry.Height - BackLabelHeight) / 2);
            return (total.Left - _body.Geometry.Left, y);
        }
    }

    /// <summary>
    ///     Gets the offset of the hexagon.
    /// </summary>
    private (double X, double Y) DiamondOffset => (Geometry.Left - _diamond.Geometry.Left, 0);

    /// <summary>
    ///     Gets the offset of the stop on the exit.
    /// </summary>
    private (double X, double Y) SpecialOffset
    {
        get
        {
            var d = _diamond.Geometry;
            var half = (d.OutY - d.InY) / 2;
            var y = Math.Max(3 * half, 4 * ActivityLayout.HexagonHalfSize);
            var x = Math.Min(BodyOffset.X - ActivityLayout.HexagonHalfSize, DiamondOffset.X) - SpecialWidth;
            return (x, y);
        }
    }

    /// <summary>
    ///     Gets the offset of the backward action.
    /// </summary>
    private (double X, double Y) BackwardOffset =>
        (Geometry.Width - _backward!.Geometry.Width, (Geometry.Height - _backward.Geometry.Height) / 2);

    /// <summary>
    ///     Creates the tile of a while loop with its arrows.
    /// </summary>
    /// <param name="layout">The layout.</param>
    /// <param name="loop">The loop.</param>
    /// <param name="body">The body.</param>
    /// <param name="diamond">The hexagon.</param>
    /// <param name="special">The stop on the exit, or null.</param>
    /// <param name="backward">The backward action, or null.</param>
    /// <returns>The tile.</returns>
    public static Tile Create(ActivityLayout layout, ActivityWhile loop, Tile body, HexagonTile diamond, Tile? special, Tile? backward)
    {
        var tile = new WhileTile(layout, loop, body, diamond, special, backward);
        var connections = new List<TileConnection>();
        var incoming1 = layout.Rainbow(loop.Incoming1);
        if (body.Geometry.Width == 0 || body.Geometry.Height == 0)
        {
            connections.Add(tile.BackEmpty(incoming1));
        }
        else
        {
            connections.Add(tile.ConnectionIn(layout.Rainbow(body.InLink)));
            if (backward is null)
            {
                connections.Add(tile.BackSimple(incoming1));
            }
            else
            {
                connections.Add(tile.BackBackward1(incoming1));
                connections.Add(tile.BackBackward2(layout.Rainbow(loop.Incoming2), layout.Label(loop.Incoming2)));
            }
        }

        var outColor = layout.Rainbow(loop.OutLink);
        connections.Add(special is null ? tile.ConnectionOut(outColor) : tile.ConnectionOutSpecial(outColor));
        return new ConnectedTile(tile, connections);
    }

    /// <inheritdoc/>
    public override void Draw(TileCanvas canvas, double x, double y)
    {
        canvas.Tile(_body, x + BodyOffset.X, y + BodyOffset.Y);
        canvas.Tile(_diamond, x + DiamondOffset.X, y + DiamondOffset.Y);
        if (_special is not null)
        {
            canvas.Tile(_special, x + SpecialOffset.X, y + SpecialOffset.Y);
        }

        if (_backward is not null)
        {
            canvas.Tile(_backward, x + BackwardOffset.X, y + BackwardOffset.Y);
        }
    }

    /// <inheritdoc/>
    protected override TileGeometry ComputeGeometry()
    {
        var d = _diamond.Geometry;
        var geo = d.AppendBottom(_body.Geometry);
        var height = geo.Height + (4 * ActivityLayout.HexagonHalfSize) + BackLabelHeight;
        const double dx = 2 * ActivityLayout.HexagonHalfSize;
        var backwardWidth = _backward?.Geometry.Width ?? 0;
        return new TileGeometry(
            SpecialWidth + geo.Width + dx + ActivityLayout.HexagonHalfSize + backwardWidth,
            height,
            SpecialWidth + geo.Left + dx,
            d.InY,
            height);
    }

    /// <summary>
    ///     Gets the y of the middle of the hexagon.
    /// </summary>
    private double DiamondMiddle => DiamondOffset.Y + _diamond.Geometry.InY + ((_diamond.Geometry.OutY - _diamond.Geometry.InY) / 2);

    /// <summary>
    ///     Gets the bottom of the body.
    /// </summary>
    private double BodyBottom => BodyOffset.Y + _body.Geometry.Height;

    /// <summary>
    ///     Creates the arrow from the hexagon into the body.
    /// </summary>
    /// <param name="link">The color.</param>
    /// <returns>The connection.</returns>
    private DelegateConnection ConnectionIn(ActivityLink link)
    {
        (double X, double Y) P1() => (DiamondOffset.X + _diamond.Geometry.Left, DiamondOffset.Y + _diamond.Geometry.OutY);
        (double X, double Y) P2() => (BodyOffset.X + _body.Geometry.Left, BodyOffset.Y + _body.Geometry.InY);
        return new DelegateConnection(
            _diamond,
            _body,
            (canvas, x, y) =>
            {
                var (p1, p2) = (P1(), P2());
                canvas.Add(new Snake(link, ArrowDirection.Down).Add(x + p1.X, y + p1.Y).Add(x + p2.X, y + p2.Y));
            },
            (canvas, x, y, dx1, dx2) =>
            {
                var (p1, p2) = (P1(), P2());
                var (x1, y1, x2, y2) = (x + p1.X + dx1, y + p1.Y, x + p2.X + dx2, y + p2.Y);
                var middle = (y1 + y2) / 2;
                canvas.Add(new Snake(link, ArrowDirection.Down).Add(x1, y1).Add(x1, middle).Add(x2, middle).Add(x2, y2));
            });
    }

    /// <summary>
    ///     Creates the arrow from the body back to the hexagon, on the right (PlantUML's <c>ConnectionBackSimple</c>).
    /// </summary>
    /// <param name="link">The color.</param>
    /// <returns>The connection.</returns>
    private DelegateConnection BackSimple(ActivityLink link) =>
        new(
            _body,
            _diamond,
            (canvas, x, y) =>
            {
                if (!_body.Geometry.HasOut)
                {
                    return;
                }

                var x1 = BodyOffset.X + _body.Geometry.Left;
                var y1 = BodyOffset.Y + _body.Geometry.OutY;
                var x2 = DiamondOffset.X + _diamond.Geometry.Width;
                var y1bis = Math.Max(y1, BodyBottom) + ActivityLayout.HexagonHalfSize;
                var xx = Geometry.Width;
                canvas.Add(new Snake(link, ArrowDirection.Left) { Emphasize = ArrowDirection.Up, Label = _back1, LabelPlace = SnakeLabelPlace.Bottom }
                    .Add(x + x1, y + y1).Add(x + x1, y + y1bis).Add(x + xx, y + y1bis).Add(x + xx, y + DiamondMiddle).Add(x + x2, y + DiamondMiddle));
                canvas.Empty(x + x1, y + y1bis, 5, ActivityLayout.HexagonHalfSize);
            },
            (canvas, x, y, dx1, dx2) =>
            {
                if (!_body.Geometry.HasOut)
                {
                    return;
                }

                var x1 = x + BodyOffset.X + _body.Geometry.Left + dx1;
                var y1 = y + BodyOffset.Y + _body.Geometry.OutY;
                var x2 = x + DiamondOffset.X + _diamond.Geometry.Width + dx2;
                var y2 = y + DiamondMiddle;
                var xx = x + Math.Max(dx1, dx2) + Geometry.Width;
                canvas.Add(new Snake(link, ArrowDirection.Left)
                    .Add(x1, y1).Add(x1, y1 + ActivityLayout.HexagonHalfSize).Add(xx, y1 + ActivityLayout.HexagonHalfSize).Add(xx, y2).Add(x2, y2));
                canvas.Empty(x1, y1 + ActivityLayout.HexagonHalfSize, 5, ActivityLayout.HexagonHalfSize);
                canvas.Add(ActivityLayout.Head(ArrowDirection.Up, xx, (y1 + y2) / 2, link.Color ?? canvas.Layout.ArrowColor, false, link.Location));
            });

    /// <summary>
    ///     Creates the arrow back of a loop without a body.
    /// </summary>
    /// <param name="link">The color.</param>
    /// <returns>The connection.</returns>
    private DelegateConnection BackEmpty(ActivityLink link) =>
        new(_diamond, _diamond, (canvas, x, y) =>
        {
            var x1 = DiamondOffset.X + _diamond.Geometry.Left;
            var y1 = DiamondOffset.Y + _diamond.Geometry.OutY;
            var x2 = DiamondOffset.X + _diamond.Geometry.Width;
            var y1bis = Math.Max(y1, BodyBottom) + ActivityLayout.HexagonHalfSize;
            var xx = Geometry.Width;
            canvas.Add(new Snake(link, ArrowDirection.Left) { Emphasize = ArrowDirection.Up }
                .Add(x + x1, y + y1).Add(x + x1, y + y1bis).Add(x + xx, y + y1bis).Add(x + xx, y + DiamondMiddle).Add(x + x2, y + DiamondMiddle));
            canvas.Empty(x + x1, y + y1bis, 5, ActivityLayout.HexagonHalfSize);
        });

    /// <summary>
    ///     Creates the arrow from the body to the backward action.
    /// </summary>
    /// <param name="link">The color.</param>
    /// <returns>The connection.</returns>
    private DelegateConnection BackBackward1(ActivityLink link) =>
        new(_body, _backward, (canvas, x, y) =>
        {
            if (!_body.Geometry.HasOut)
            {
                return;
            }

            var x1 = BodyOffset.X + _body.Geometry.Left;
            var y1 = BodyOffset.Y + _body.Geometry.OutY;
            var back = _backward!.Geometry;
            var x2 = BackwardOffset.X + back.Left;
            var y2 = BackwardOffset.Y + back.OutY;
            var y1bis = Math.Max(y1, BodyBottom) + ActivityLayout.HexagonHalfSize;
            canvas.Add(new Snake(link, ArrowDirection.Up) { Label = _back1, LabelPlace = SnakeLabelPlace.Bottom }
                .Add(x + x1, y + y1).Add(x + x1, y + y1bis).Add(x + x2, y + y1bis).Add(x + x2, y + y2));
            canvas.Empty(x + x1, y + y1bis, 5, ActivityLayout.HexagonHalfSize);
        });

    /// <summary>
    ///     Creates the arrow from the backward action to the hexagon.
    /// </summary>
    /// <param name="link">The color.</param>
    /// <param name="label">The label.</param>
    /// <returns>The connection.</returns>
    private DelegateConnection BackBackward2(ActivityLink link, TextBlock? label) =>
        new(_backward, _diamond, (canvas, x, y) =>
        {
            var back = _backward!.Geometry;
            var x1 = BackwardOffset.X + back.Left;
            var y1 = BackwardOffset.Y + back.InY;
            var x2 = DiamondOffset.X + _diamond.Geometry.Width;
            canvas.Add(new Snake(link, ArrowDirection.Left) { Label = label }.Add(x + x1, y + y1).Add(x + x1, y + DiamondMiddle).Add(x + x2, y + DiamondMiddle));
        });

    /// <summary>
    ///     Creates the exit arrow on the left of the loop down to its bottom.
    /// </summary>
    /// <param name="link">The color.</param>
    /// <returns>The connection.</returns>
    private DelegateConnection ConnectionOut(ActivityLink link) =>
        new(_diamond, null, (canvas, x, y) =>
        {
            var x1 = DiamondOffset.X;
            const double side = ActivityLayout.HexagonHalfSize;
            var (x2, y2) = (Geometry.Left, Geometry.Height);
            canvas.Add(new Snake(link, ArrowDirection.None) { Emphasize = ArrowDirection.Down, Merge = SnakeMerge.Limited }.Add(x + x1, y + DiamondMiddle).Add(x + side, y + DiamondMiddle).Add(x + side, y + y2));
            canvas.Add(new Snake(link, ArrowDirection.None).Add(x + side, y + y2).Add(x + x2, y + y2));
        });

    /// <summary>
    ///     Creates the exit arrow to the stop on the left.
    /// </summary>
    /// <param name="link">The color.</param>
    /// <returns>The connection.</returns>
    private DelegateConnection ConnectionOutSpecial(ActivityLink link) =>
        new(_diamond, _special, (canvas, x, y) =>
        {
            var x1 = DiamondOffset.X;
            var special = _special!.Geometry;
            var (x2, y2) = (SpecialOffset.X + special.Left, SpecialOffset.Y + special.InY);
            canvas.Add(new Snake(link, ArrowDirection.Down).Add(x + x1, y + DiamondMiddle).Add(x + x2, y + DiamondMiddle).Add(x + x2, y + y2));
        });
}

/// <summary>
///     A repeat loop (PlantUML's <c>FtileRepeat</c>): the start diamond (or action) above the body, the test hexagon
///     below, and the arrow back on the right with an upward head.
/// </summary>
internal sealed class RepeatTile : Tile
{
    /// <summary>The body.</summary>
    private readonly Tile _repeat;

    /// <summary>The start diamond or action.</summary>
    private readonly Tile _diamond1;

    /// <summary>The test hexagon, or empty space.</summary>
    private readonly Tile _diamond2;

    /// <summary>The backward action, or null.</summary>
    private readonly Tile? _backward;

    /// <summary>The loop.</summary>
    private readonly ActivityRepeat _loop;

    /// <summary>
    ///     Initializes the tile.
    /// </summary>
    /// <param name="layout">The layout.</param>
    /// <param name="loop">The loop.</param>
    /// <param name="repeat">The body.</param>
    /// <param name="diamond1">The start.</param>
    /// <param name="diamond2">The test.</param>
    /// <param name="backward">The backward action.</param>
    private RepeatTile(ActivityLayout layout, ActivityRepeat loop, Tile repeat, Tile diamond1, Tile diamond2, Tile? backward)
        : base(layout)
    {
        _loop = loop;
        _repeat = repeat;
        _diamond1 = diamond1;
        _diamond2 = diamond2;
        _backward = backward;
    }

    /// <inheritdoc/>
    public override ActivityLane? LaneIn => _loop.Lane;

    /// <inheritdoc/>
    public override ActivityLane? LaneOut => _loop.LaneOut;

    /// <inheritdoc/>
    protected override IReadOnlySet<ActivityLane> ComputeLanes() => MergeLanes(_repeat, _diamond1, _diamond2, _backward);

    /// <summary>
    ///     Gets the x of the in and out points.
    /// </summary>
    private double Left
    {
        get
        {
            var left = _repeat.Geometry.Left;
            return Math.Max(Math.Max(left, _diamond1.Geometry.Width / 2), _diamond2.Geometry.Width / 2);
        }
    }

    /// <summary>
    ///     Gets the size of the loop.
    /// </summary>
    private (double Width, double Height) Internal
    {
        get
        {
            var repeat = _repeat.Geometry;
            var right = Math.Max(Math.Max(repeat.Width - repeat.Left, _diamond1.Geometry.Width / 2), _diamond2.Geometry.Width / 2);
            var width = Left + right;
            if (_backward is not null)
            {
                width += _backward.Geometry.Width;
            }

            var height = _diamond1.Geometry.Height + repeat.Height + _diamond2.Geometry.Height + (8 * ActivityLayout.HexagonHalfSize);
            return (width + (2 * ActivityLayout.HexagonHalfSize), height);
        }
    }

    /// <summary>
    ///     Gets the offset of the body.
    /// </summary>
    private (double X, double Y) RepeatOffset
    {
        get
        {
            var space = Internal.Height - _diamond1.Geometry.Height - _diamond2.Geometry.Height - _repeat.Geometry.Height;
            return (Left - _repeat.Geometry.Left, _diamond1.Geometry.Height + (space / 2));
        }
    }

    /// <summary>
    ///     Gets the offset of the start.
    /// </summary>
    private (double X, double Y) Diamond1Offset => (Left - (_diamond1.Geometry.Width / 2), 0);

    /// <summary>
    ///     Gets the offset of the test.
    /// </summary>
    private (double X, double Y) Diamond2Offset => (Left - (_diamond2.Geometry.Width / 2), Internal.Height - _diamond2.Geometry.Height);

    /// <summary>
    ///     Gets the offset of the backward action.
    /// </summary>
    private (double X, double Y) BackwardOffset =>
        (Internal.Width - _backward!.Geometry.Width, (Internal.Height - _backward.Geometry.Height) / 2);

    /// <summary>
    ///     Creates the tile of a repeat loop with its arrows.
    /// </summary>
    /// <param name="layout">The layout.</param>
    /// <param name="loop">The loop.</param>
    /// <param name="entry">The start diamond or action.</param>
    /// <param name="repeat">The body.</param>
    /// <param name="exit">The test.</param>
    /// <param name="backward">The backward action.</param>
    /// <returns>The tile.</returns>
    public static Tile Create(ActivityLayout layout, ActivityRepeat loop, Tile entry, Tile repeat, Tile exit, Tile? backward)
    {
        var tile = new RepeatTile(layout, loop, repeat, entry, exit, backward);
        var incoming1 = layout.Rainbow(loop.Incoming1);
        var incomingText = layout.Label(loop.Incoming1);
        var connections = new List<TileConnection> { tile.ConnectionIn(layout.Rainbow(repeat.InLink), layout.Label(repeat.InLink)) };
        if (backward is not null)
        {
            connections.Add(tile.BackBackward1(incoming1, incomingText));
            connections.Add(tile.BackBackward2(layout.Rainbow(loop.Incoming2), layout.Label(loop.Incoming2)));
        }
        else if (loop.Lane is null || loop.Lane == loop.LaneOut)
        {
            var left = loop.Lane is { } lane && IfDownTile.IsSmallerThanAll(lane, repeat.Lanes);
            connections.Add(tile.BackSimple(incoming1, incomingText, left));
        }
        else
        {
            connections.Add(tile.BackSimple(incoming1, null, false));
        }

        connections.Add(tile.ConnectionOut(layout.Rainbow(repeat.OutLink), layout.Label(repeat.OutLink)));
        return new ConnectedTile(tile, connections);
    }

    /// <inheritdoc/>
    public override void Draw(TileCanvas canvas, double x, double y)
    {
        canvas.Tile(_repeat, x + RepeatOffset.X, y + RepeatOffset.Y);
        canvas.Tile(_diamond1, x + Diamond1Offset.X, y + Diamond1Offset.Y);
        canvas.Tile(_diamond2, x + Diamond2Offset.X, y + Diamond2Offset.Y);
        if (_backward is not null)
        {
            canvas.Tile(_backward, x + BackwardOffset.X, y + BackwardOffset.Y);
        }
    }

    /// <inheritdoc/>
    protected override TileGeometry ComputeGeometry()
    {
        var (width, height) = Internal;
        return new TileGeometry(width, height, Left, 0, height);
    }

    /// <summary>
    ///     Creates the arrow from the start into the body.
    /// </summary>
    /// <param name="link">The color.</param>
    /// <param name="label">The label.</param>
    /// <returns>The connection.</returns>
    private DelegateConnection ConnectionIn(ActivityLink link, TextBlock? label)
    {
        (double X, double Y) P1() => (Diamond1Offset.X + _diamond1.Geometry.Left, Diamond1Offset.Y + _diamond1.Geometry.OutY);
        (double X, double Y) P2() => (RepeatOffset.X + _repeat.Geometry.Left, RepeatOffset.Y + _repeat.Geometry.InY);
        void Draw(TileCanvas canvas, (double X, double Y) p1, (double X, double Y) p2)
        {
            var snake = new Snake(link, ArrowDirection.Down) { Label = label }.Add(p1.X, p1.Y);
            if (Math.Abs(p1.X - p2.X) > 1e-9)
            {
                var middle = (p1.Y + p2.Y) / 2;
                snake.Add(p1.X, middle).Add(p2.X, middle);
            }

            canvas.Add(snake.Add(p2.X, p2.Y));
        }

        return new DelegateConnection(
            _diamond1,
            _repeat,
            (canvas, x, y) =>
            {
                var (p1, p2) = (P1(), P2());
                Draw(canvas, (x + p1.X, y + p1.Y), (x + p2.X, y + p2.Y));
            },
            (canvas, x, y, dx1, dx2) =>
            {
                var (p1, p2) = (P1(), P2());
                Draw(canvas, (x + p1.X + dx1, y + p1.Y), (x + p2.X + dx2, y + p2.Y));
            });
    }

    /// <summary>
    ///     Creates the arrow from the body to the test.
    /// </summary>
    /// <param name="link">The color.</param>
    /// <param name="label">The label.</param>
    /// <returns>The connection.</returns>
    private DelegateConnection ConnectionOut(ActivityLink link, TextBlock? label)
    {
        (double X, double Y) P1() => (RepeatOffset.X + _repeat.Geometry.Left, RepeatOffset.Y + _repeat.Geometry.OutY);
        (double X, double Y) P2() => (Diamond2Offset.X + _diamond2.Geometry.Left, Diamond2Offset.Y + _diamond2.Geometry.InY);
        return new DelegateConnection(
            _repeat,
            _diamond2,
            (canvas, x, y) =>
            {
                if (!_repeat.Geometry.HasOut)
                {
                    return;
                }

                var (p1, p2) = (P1(), P2());
                canvas.Add(new Snake(link, ArrowDirection.Down) { Label = label }.Add(x + p1.X, y + p1.Y).Add(x + p2.X, y + p2.Y));
            },
            (canvas, x, y, dx1, dx2) =>
            {
                if (!_repeat.Geometry.HasOut)
                {
                    return;
                }

                var (p1, p2) = (P1(), P2());
                var (x1, y1, x2, y2) = (x + p1.X + dx1, y + p1.Y, x + p2.X + dx2, y + p2.Y);
                var middle = (y1 + y2) / 2;
                canvas.Add(new Snake(link, ArrowDirection.None).Add(x1, y1).Add(x1, middle).Add(x2, middle));
                canvas.Add(new Snake(link, ArrowDirection.Down) { Label = label }.Add(x2, middle).Add(x2, y2));
            });
    }

    /// <summary>
    ///     Creates the arrow from the test back to the start (PlantUML's <c>ConnectionBackSimple1</c> on the left and
    ///     <c>ConnectionBackSimple2</c> on the right).
    /// </summary>
    /// <param name="link">The color.</param>
    /// <param name="label">The label.</param>
    /// <param name="onLeft">Whether the arrow goes back on the left.</param>
    /// <returns>The connection.</returns>
    private DelegateConnection BackSimple(ActivityLink link, TextBlock? label, bool onLeft) =>
        new(
            _diamond2,
            _diamond1,
            (canvas, x, y) =>
            {
                var (d1, d2) = (_diamond1.Geometry, _diamond2.Geometry);
                var y1 = Diamond2Offset.Y + (d2.Height / 2);
                var y2 = Diamond1Offset.Y + (d1.Height / 2);
                Snake snake;
                if (onLeft)
                {
                    const double side = -ActivityLayout.HexagonHalfSize;
                    snake = new Snake(link, ArrowDirection.Right) { Emphasize = ArrowDirection.Up, Label = label }
                        .Add(x + Diamond2Offset.X, y + y1).Add(x + side, y + y1).Add(x + side, y + y2).Add(x + Diamond1Offset.X, y + y2);
                }
                else
                {
                    var side = Internal.Width - ActivityLayout.HexagonHalfSize;
                    snake = new Snake(link, ArrowDirection.Left) { Emphasize = ArrowDirection.Up, Label = label }
                        .Add(x + Diamond2Offset.X + d2.Width, y + y1).Add(x + side, y + y1).Add(x + side, y + y2).Add(x + Diamond1Offset.X + d1.Width, y + y2);
                }

                canvas.Add(snake);
            },
            (canvas, x, y, dx1, dx2) =>
            {
                var (d1, d2) = (_diamond1.Geometry, _diamond2.Geometry);
                var x1 = x + Diamond2Offset.X + d2.Width + dx1;
                var y1 = y + Diamond2Offset.Y + (d2.Height / 2);
                var x2a = x + Diamond1Offset.X + dx2;
                var x2b = x2a + d1.Width;
                var y2 = y + Diamond1Offset.Y + (d1.Height / 2);
                var onA = x1 < (x2a + x2b) / 2;
                var x2 = onA ? x2a : x2b;
                var middle = (x1 + x2) / 2;
                canvas.Add(new Snake(link, onA ? ArrowDirection.Right : ArrowDirection.Left) { Emphasize = ArrowDirection.Up, Label = label }
                    .Add(x1, y1).Add(middle, y1).Add(middle, y2).Add(x2, y2));
            });

    /// <summary>
    ///     Creates the arrow from the test to the backward action.
    /// </summary>
    /// <param name="link">The color.</param>
    /// <param name="label">The label.</param>
    /// <returns>The connection.</returns>
    private DelegateConnection BackBackward1(ActivityLink link, TextBlock? label) =>
        new(_diamond2, _backward, (canvas, x, y) =>
        {
            var d2 = _diamond2.Geometry;
            var back = _backward!.Geometry;
            var x2 = BackwardOffset.X + back.Left;
            var y2 = BackwardOffset.Y + back.OutY;
            var center = Diamond2Offset.X + (d2.Width / 2);
            var x1 = x2 < center ? Diamond2Offset.X : Diamond2Offset.X + d2.Width;
            var y1 = Diamond2Offset.Y + (d2.Height / 2);
            canvas.Add(new Snake(link, ArrowDirection.Up) { Label = label }.Add(x + x1, y + y1).Add(x + x2, y + y1).Add(x + x2, y + y2));
        });

    /// <summary>
    ///     Creates the arrow from the backward action to the start.
    /// </summary>
    /// <param name="link">The color.</param>
    /// <param name="label">The label.</param>
    /// <returns>The connection.</returns>
    private DelegateConnection BackBackward2(ActivityLink link, TextBlock? label) =>
        new(_backward, _diamond1, (canvas, x, y) =>
        {
            var back = _backward!.Geometry;
            var d1 = _diamond1.Geometry;
            var x1 = BackwardOffset.X + back.Left;
            var y1 = BackwardOffset.Y + back.InY;
            var x2 = Diamond1Offset.X + d1.Width;
            var y2 = Diamond1Offset.Y + (d1.Height / 2);
            canvas.Add(new Snake(link, ArrowDirection.Left) { Label = label }.Add(x + x1, y + y1).Add(x + x1, y + y2).Add(x + x2, y + y2));
        });
}
