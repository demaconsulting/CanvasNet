// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;

namespace DemaConsulting.CanvasNet.PlantUml.Activity;

/// <content>
///     The ifs: two branches side by side under a hexagon (PlantUML's <c>ConditionalBuilder</c> and
///     <c>FtileIfWithLinks</c>), a branch below a hexagon with the other beside it (<c>FtileIfDown</c>), and the
///     elseif chain (<c>FtileIfLongHorizontal</c>).
/// </content>
internal sealed partial class ActivityLayout
{
    /// <summary>
    ///     Builds the tile of an if.
    /// </summary>
    /// <param name="block">The if.</param>
    /// <returns>The tile.</returns>
    private Tile BuildIf(ActivityIf block)
    {
        var elseBranch = block.Else ?? new ActivityBranch(block.Lane, ActivityLink.None, string.Empty, null, ActivityLink.None);
        var thens = block.Thens.Select(b => (Branch: b, Tile: BranchTile(b))).ToList();
        var other = (Branch: elseBranch, Tile: BranchTile(elseBranch));
        var fill = block.Thens[0].Color ?? DiamondFill;
        return thens.Count > 1
            ? IfLongHorizontalTile.Create(this, block, thens, other, fill)
            : BuildConditional(block, thens[0], other, fill);
    }

    /// <summary>
    ///     Builds the tile of a branch: its list, with the arrow that leaves it (PlantUML's <c>Branch.updateFtile</c>).
    /// </summary>
    /// <param name="branch">The branch.</param>
    /// <returns>The tile.</returns>
    private Tile BranchTile(ActivityBranch branch) => new DecoratedTile(BuildList(branch.List), outLink: branch.InLinkRendering);

    /// <summary>
    ///     Builds an if with two branches (PlantUML's <c>ConditionalBuilder.create</c>): below the hexagon when a
    ///     branch is empty or only a stop, side by side otherwise.
    /// </summary>
    /// <param name="block">The if.</param>
    /// <param name="branch1">The then branch and its tile.</param>
    /// <param name="branch2">The else branch and its tile.</param>
    /// <param name="fill">The fill of the hexagon.</param>
    /// <returns>The tile.</returns>
    private Tile BuildConditional(ActivityIf block, (ActivityBranch Branch, Tile Tile) branch1, (ActivityBranch Branch, Tile Tile) branch2, Rgba32? fill)
    {
        static bool EmptyOrStop(ActivityBranch b) => b.IsEmpty || b.List.IsOnlySingleStopOrSpot;
        var twoOuts = branch1.Tile.Geometry.HasOut && branch2.Tile.Geometry.HasOut;
        if ((EmptyOrStop(branch2.Branch) && !EmptyOrStop(branch1.Branch)) || (branch1.Branch.IsEmpty && branch2.Branch.List.IsOnlySingleStopOrSpot))
        {
            return CreateDown(block, branch1, branch2, fill, twoOuts);
        }

        if ((EmptyOrStop(branch1.Branch) && !EmptyOrStop(branch2.Branch)) || (branch2.Branch.IsEmpty && branch1.Branch.List.IsOnlySingleStopOrSpot))
        {
            return CreateDown(block, branch2, branch1, fill, twoOuts);
        }

        return IfWithLinksTile.Create(this, block, branch1, branch2, fill, twoOuts);
    }

    /// <summary>
    ///     Creates the test hexagon of an if.
    /// </summary>
    /// <param name="block">The if.</param>
    /// <param name="test">The test.</param>
    /// <returns>The laid-out test.</returns>
    internal TextBlock TestTextOf(ActivityIf block, string test) =>
        test.Length == 0 ? TextBlock.Empty : Text(test, SkinElement.ActivityDiamond, block.Location);

    /// <summary>
    ///     Creates the merge diamond of an if (PlantUML's <c>ConditionalBuilder.getShape2</c>): a diamond when both
    ///     branches continue, otherwise 6 pixels of space.
    /// </summary>
    /// <param name="block">The if.</param>
    /// <param name="branch1">The first branch.</param>
    /// <param name="branch2">The second branch.</param>
    /// <param name="fill">The fill of the if's hexagon.</param>
    /// <param name="twoOuts">Whether both branches continue.</param>
    /// <param name="useNorth">Whether the label of the first branch's exit is drawn above (else left of) the diamond.</param>
    /// <returns>The tile.</returns>
    internal Tile MergeDiamondOf(ActivityIf block, (ActivityBranch Branch, Tile Tile) branch1, (ActivityBranch Branch, Tile Tile) branch2, Rgba32? fill, bool twoOuts, bool useNorth)
    {
        if (!twoOuts)
        {
            return new EmptyTile(this, 0, HexagonHalfSize / 2, block.Lane);
        }

        var out1 = Label(branch1.Tile.OutLink);
        var out2 = Label(branch2.Tile.OutLink);
        return new DiamondTile(this, branch2.Branch.Color ?? fill, block.Lane)
        {
            North = useNorth ? out1 : null,
            West = useNorth ? null : out1,
            East = out2,
            Location = block.Location,
        };
    }

    /// <summary>
    ///     Builds an if whose second branch is empty or only a stop (PlantUML's <c>ConditionalBuilder.createDown</c>).
    /// </summary>
    /// <param name="block">The if.</param>
    /// <param name="down">The branch drawn below the hexagon.</param>
    /// <param name="beside">The other branch.</param>
    /// <param name="fill">The fill of the hexagon.</param>
    /// <param name="twoOuts">Whether both original branches continue.</param>
    /// <returns>The tile.</returns>
    private Tile CreateDown(ActivityIf block, (ActivityBranch Branch, Tile Tile) down, (ActivityBranch Branch, Tile Tile) beside, Rgba32? fill, bool twoOuts)
    {
        var tile1 = new MinWidthTile(down.Tile, 30);
        var tile2 = new MinWidthTile(beside.Tile, 30);
        var tb1 = PositiveLabelOf(down.Branch, down.Tile);
        var tb2 = PositiveLabelOf(beside.Branch, beside.Tile);
        var test = TestTextOf(block, block.Thens[0].Test);
        var diamond1 = new HexagonTile(this, test, fill, block.Lane) { South = tb1, East = tb2, Location = block.Location };
        var original1 = block.Thens[0] == down.Branch ? down : beside;
        var original2 = block.Thens[0] == down.Branch ? beside : down;
        var diamond2 = MergeDiamondOf(block, original1, original2, fill, twoOuts, true);
        if (beside.Branch.List.IsOnlySingleStopOrSpot)
        {
            return IfDownTile.Create(this, block, diamond1, diamond2, MarginTile.Of(tile1, 10, 10), beside.Tile, BranchOutOf(beside.Branch));
        }

        if (down.Branch.List.IsOnlySingleStopOrSpot)
        {
            return IfDownTile.Create(this, block, diamond1, diamond2, MarginTile.Of(tile2, 10, 10), down.Tile, BranchOutOf(down.Branch));
        }

        if (down.Branch.IsEmpty)
        {
            return IfDownTile.Create(this, block, diamond1, diamond2, MarginTile.Of(tile2, 10, 10), null, Rainbow(null));
        }

        return IfDownTile.Create(this, block, diamond1, diamond2, MarginTile.Of(tile1, 10, 10), null, BranchOutOf(beside.Branch));
    }
}

/// <summary>
///     An if with two branches side by side under its hexagon and a merge diamond below (PlantUML's
///     <c>FtileIfNude</c>, <c>FtileIfWithDiamonds</c> and <c>FtileIfWithLinks</c>).
/// </summary>
internal sealed class IfWithLinksTile : Tile
{
    /// <summary>The smallest space beside the hexagon.</summary>
    private const double SuppWidth = 20;

    /// <summary>The hexagon.</summary>
    private readonly Tile _diamond1;

    /// <summary>The then branch with its margins.</summary>
    private readonly Tile _tile1;

    /// <summary>The else branch with its margins.</summary>
    private readonly Tile _tile2;

    /// <summary>The merge diamond, or the space below.</summary>
    private readonly Tile _diamond2;

    /// <summary>The if.</summary>
    private readonly ActivityIf _block;

    /// <summary>The note on the left of the hexagon, or null.</summary>
    private readonly NoteShape? _noteLeft;

    /// <summary>The note on the right of the hexagon, or null.</summary>
    private readonly NoteShape? _noteRight;

    /// <summary>The space left of the branches for a left note.</summary>
    private readonly double _xDeltaNote;

    /// <summary>The space above for the notes.</summary>
    private readonly double _yDeltaNote;

    /// <summary>The space right of the branches for a right note.</summary>
    private readonly double _suppWidthNote;

    /// <summary>
    ///     Initializes the tile.
    /// </summary>
    /// <param name="layout">The layout.</param>
    /// <param name="block">The if.</param>
    /// <param name="diamond1">The hexagon.</param>
    /// <param name="tile1">The then branch with its margins.</param>
    /// <param name="tile2">The else branch with its margins.</param>
    /// <param name="diamond2">The merge diamond.</param>
    private IfWithLinksTile(ActivityLayout layout, ActivityIf block, Tile diamond1, Tile tile1, Tile tile2, Tile diamond2)
        : base(layout)
    {
        _block = block;
        _diamond1 = diamond1;
        _tile1 = tile1;
        _tile2 = tile2;
        _diamond2 = diamond2;
        foreach (var note in block.Notes)
        {
            if (note.Position == ActivityNotePosition.Left && _noteLeft is null)
            {
                _noteLeft = new NoteShape(layout, note);
                var pos1 = Diamond1Offset.X;
                if (_noteLeft.Width > pos1)
                {
                    _xDeltaNote = _noteLeft.Width - pos1;
                }

                _yDeltaNote = Math.Max(_yDeltaNote, _noteLeft.Height);
            }
            else if (note.Position == ActivityNotePosition.Right && _noteRight is null)
            {
                _noteRight = new NoteShape(layout, note);
                var pos1 = Diamond1Offset.X + _diamond1.Geometry.Width + _noteRight.Width;
                var pos2 = Internal.Width;
                if (pos1 > pos2)
                {
                    _suppWidthNote = pos1 - pos2;
                }

                _yDeltaNote = Math.Max(_yDeltaNote, _noteRight.Height);
            }
        }
    }

    /// <inheritdoc/>
    public override ActivityLane? LaneIn => _block.Lane;

    /// <inheritdoc/>
    protected override IReadOnlySet<ActivityLane> ComputeLanes()
    {
        var lanes = new HashSet<ActivityLane>(MergeLanes(_tile1, _tile2));
        if (_block.Lane is { } lane)
        {
            lanes.Add(lane);
        }

        return lanes;
    }

    /// <summary>
    ///     Gets a value indicating whether both branches continue.
    /// </summary>
    private bool TwoBranches => _tile1.Geometry.HasOut && _tile2.Geometry.HasOut;

    /// <summary>
    ///     Gets the space between the hexagon and the branches.
    /// </summary>
    private double Delta1a => Lanes.Count > 1 ? 20 : 10;

    /// <summary>
    ///     Gets the space between the branches and the merge.
    /// </summary>
    private double Delta1b
    {
        get
        {
            if (Lanes.Count > 1)
            {
                return 10;
            }

            return TwoBranches ? 6 : 0;
        }
    }

    /// <summary>
    ///     Gets the height of the labels of the merge diamond.
    /// </summary>
    private double DeltaForLabels => _diamond2 is DiamondTile diamond ? diamond.WestEastLabelHeight : 0;

    /// <summary>
    ///     Gets the width between the in points of the branches.
    /// </summary>
    private double WidthInner
    {
        get
        {
            var (dim1, dim2) = (_tile1.Geometry, _tile2.Geometry);
            return Math.Max((dim1.Width - dim1.Left) + dim2.Left, _diamond1.Geometry.Width + SuppWidth);
        }
    }

    /// <summary>
    ///     Gets the geometry of the branches alone (PlantUML's <c>FtileIfNude</c>).
    /// </summary>
    private TileGeometry Nude
    {
        get
        {
            var (dim1, dim2) = (_tile1.Geometry, _tile2.Geometry);
            var inner = WidthInner;
            var width = _xDeltaNote + dim1.Left + inner + (dim2.Width - dim2.Left) + _suppWidthNote;
            var height = _yDeltaNote + Math.Max(dim1.Height, dim2.Height);
            return new TileGeometry(width, height, _xDeltaNote + dim1.Left + (inner / 2), _yDeltaNote, height);
        }
    }

    /// <summary>
    ///     Gets the geometry of the whole if, with an out point (PlantUML's <c>calculateDimensionInternal</c>).
    /// </summary>
    private TileGeometry Internal
    {
        get
        {
            var all = _diamond1.Geometry.AppendBottom(Nude).AppendBottom(_diamond2.Geometry);
            var delta = Delta1a + Delta1b + DeltaForLabels;
            var result = all.AddDim(0, delta);
            return result with { InY = result.InY + _yDeltaNote };
        }
    }

    /// <summary>
    ///     Gets the offset of the hexagon.
    /// </summary>
    private (double X, double Y) Diamond1Offset => (Internal.Left - _diamond1.Geometry.Left, _yDeltaNote);

    /// <summary>
    ///     Gets the offset of the merge diamond.
    /// </summary>
    private (double X, double Y) Diamond2Offset => (Internal.Left - (_diamond2.Geometry.Width / 2), Internal.Height - _diamond2.Geometry.Height);

    /// <summary>
    ///     Gets the offset of the then branch.
    /// </summary>
    private (double X, double Y) Branch1Offset => (_xDeltaNote, _yDeltaNote + _diamond1.Geometry.Height + Delta1a);

    /// <summary>
    ///     Gets the offset of the else branch.
    /// </summary>
    private (double X, double Y) Branch2Offset =>
        (Internal.Width - _tile2.Geometry.Width - _suppWidthNote, _yDeltaNote + _diamond1.Geometry.Height + Delta1a);

    /// <summary>
    ///     Creates the tile of an if with its branches side by side, with its arrows and margins for the labels.
    /// </summary>
    /// <param name="layout">The layout.</param>
    /// <param name="block">The if.</param>
    /// <param name="branch1">The then branch.</param>
    /// <param name="branch2">The else branch.</param>
    /// <param name="fill">The fill of the hexagon.</param>
    /// <param name="twoOuts">Whether both branches continue.</param>
    /// <returns>The tile.</returns>
    public static Tile Create(ActivityLayout layout, ActivityIf block, (ActivityBranch Branch, Tile Tile) branch1, (ActivityBranch Branch, Tile Tile) branch2, Rgba32? fill, bool twoOuts)
    {
        var label1 = layout.PositiveLabelOf(branch1.Branch, branch1.Tile);
        var label2 = layout.PositiveLabelOf(branch2.Branch, branch2.Tile);
        var diamond1 = new HexagonTile(layout, layout.TestTextOf(block, block.Thens[0].Test), fill, block.Lane)
        {
            West = label1,
            East = label2,
            Location = block.Location,
        };
        var diamond2 = layout.MergeDiamondOf(block, branch1, branch2, fill, twoOuts, false);
        var tile1 = MarginTile.Of(new MinWidthTile(branch1.Tile, 30), 10, 10);
        var tile2 = MarginTile.Of(new MinWidthTile(branch2.Tile, 30), 10, 10);
        var tile = new IfWithLinksTile(layout, block, diamond1, tile1, tile2, diamond2);

        // Margins for the labels of the branch arrows
        var diff1 = Math.Max(0, (label1?.Width ?? 0) - tile.Diamond1Offset.X);
        var diff2 = Math.Max(0, tile.Diamond1Offset.X + diamond1.Geometry.Width + (label2?.Width ?? 0) - tile.Geometry.Width);
        var suppHeight = Math.Max(0, Math.Max(label1?.Height ?? 0, label2?.Height ?? 0) - diamond1.Geometry.Height);
        Tile result = new ConnectedTile(tile, tile.Connections(branch1, branch2));
        result = MarginTile.Of(result, diff1, diff2);
        return VerticalMarginTile.Of(result, suppHeight, 0);
    }

    /// <inheritdoc/>
    public override void Draw(TileCanvas canvas, double x, double y)
    {
        if (_noteLeft is { } left)
        {
            left.Draw(canvas, x + Diamond1Offset.X - left.Width, y, null);
        }

        if (_noteRight is { } right)
        {
            right.Draw(canvas, x + Diamond1Offset.X + _diamond1.Geometry.Width, y, null);
        }

        canvas.Tile(_diamond1, x + Diamond1Offset.X, y + Diamond1Offset.Y);
        canvas.Tile(_tile1, x + Branch1Offset.X, y + Branch1Offset.Y);
        canvas.Tile(_tile2, x + Branch2Offset.X, y + Branch2Offset.Y);
        canvas.Tile(_diamond2, x + Diamond2Offset.X, y + Diamond2Offset.Y);
    }

    /// <inheritdoc/>
    protected override TileGeometry ComputeGeometry() =>
        _tile1.Geometry.HasOut || _tile2.Geometry.HasOut ? Internal : Internal.WithoutOut();

    /// <summary>
    ///     Creates the arrows of the if: from the hexagon to each branch, and from each branch to the merge.
    /// </summary>
    /// <param name="branch1">The then branch.</param>
    /// <param name="branch2">The else branch.</param>
    /// <returns>The connections.</returns>
    private List<TileConnection> Connections((ActivityBranch Branch, Tile Tile) branch1, (ActivityBranch Branch, Tile Tile) branch2)
    {
        var connections = new List<TileConnection>
        {
            In(_tile1, branch1, true),
            In(_tile2, branch2, false),
        };
        var (out1, out2) = (_tile1.Geometry.HasOut, _tile2.Geometry.HasOut);
        if (out1 && out2)
        {
            connections.Add(Out(_tile1, branch1.Branch, true));
            connections.Add(Out(_tile2, branch2.Branch, false));
        }
        else if (out1)
        {
            connections.Add(OutDirect(_tile1, branch1.Branch, true));
        }
        else if (out2)
        {
            connections.Add(OutDirect(_tile2, branch2.Branch, false));
        }

        return connections;
    }

    /// <summary>
    ///     Creates the arrow from the hexagon to a branch (PlantUML's <c>ConnectionHorizontalThenVertical</c>).
    /// </summary>
    /// <param name="tile">The branch tile.</param>
    /// <param name="branch">The branch.</param>
    /// <param name="first">Whether it is the then branch (which leaves the hexagon on its left).</param>
    /// <returns>The connection.</returns>
    private DelegateConnection In(Tile tile, (ActivityBranch Branch, Tile Tile) branch, bool first)
    {
        var link = Layout.BranchInColorOf(branch.Branch, branch.Tile);
        var head = branch.Branch.IsEmpty ? ArrowDirection.None : ArrowDirection.Down;
        (double X, double Y) P1()
        {
            var point = first ? _diamond1.Geometry.PointD : _diamond1.Geometry.PointB;
            return (Diamond1Offset.X + point.X, Diamond1Offset.Y + point.Y);
        }

        (double X, double Y) P2()
        {
            var offset = first ? Branch1Offset : Branch2Offset;
            return (offset.X + tile.Geometry.Left, offset.Y + tile.Geometry.InY);
        }

        return new DelegateConnection(
            _diamond1,
            tile,
            (canvas, x, y) =>
            {
                var (p1, p2) = (P1(), P2());
                canvas.Add(new Snake(link, head).Add(x + p1.X, y + p1.Y).Add(x + p2.X, y + p1.Y).Add(x + p2.X, y + p2.Y));
            },
            (canvas, x, y, dx1, dx2) =>
            {
                var (p1, p2) = (P1(), P2());
                var original = p2.X >= p1.X;
                var (x1, y1) = (x + p1.X + dx1, y + p1.Y);
                var (x2, y2) = (x + p2.X + dx2, y + p2.Y);
                if (original != (x2 >= x1))
                {
                    var delta = (original ? -1 : 1) * ActivityLayout.HexagonHalfSize;
                    canvas.Add(new Snake(link, ArrowDirection.None).Add(x1, y1).Add(x1 + delta, y1).Add(x1 + delta, y1 + (_diamond1.Geometry.Height * .75)));
                    (x1, y1) = (x1 + delta, y1 + (_diamond1.Geometry.Height * .75));
                }

                canvas.Add(new Snake(link, head).Add(x1, y1).Add(x2, y1).Add(x2, y2));
            });
    }

    /// <summary>
    ///     Creates the arrow from a branch to the merge diamond (PlantUML's <c>ConnectionVerticalThenHorizontal</c>).
    /// </summary>
    /// <param name="tile">The branch tile.</param>
    /// <param name="branch">The branch.</param>
    /// <param name="first">Whether it is the then branch (which reaches the diamond on its left).</param>
    /// <returns>The connection.</returns>
    private DelegateConnection Out(Tile tile, ActivityBranch branch, bool first)
    {
        var link = Layout.BranchOutOf(branch);
        (double X, double Y) P1()
        {
            var offset = first ? Branch1Offset : Branch2Offset;
            return (offset.X + tile.Geometry.Left, offset.Y + tile.Geometry.OutY);
        }

        (double X, double Y) P2()
        {
            var point = first ? _diamond2.Geometry.PointD : _diamond2.Geometry.PointB;
            return (Diamond2Offset.X + point.X, Diamond2Offset.Y + point.Y);
        }

        return new DelegateConnection(
            tile,
            _diamond2,
            (canvas, x, y) =>
            {
                var (p1, p2) = (P1(), P2());
                var head = p2.X > p1.X ? ArrowDirection.Right : ArrowDirection.Left;
                var snake = new Snake(link, head) { Emphasize = branch.IsEmpty ? ArrowDirection.Down : ArrowDirection.None };
                canvas.Add(snake.Add(x + p1.X, y + p1.Y).Add(x + p1.X, y + p2.Y).Add(x + p2.X, y + p2.Y));
            },
            (canvas, x, y, dx1, dx2) =>
            {
                var (p1, p2) = (P1(), P2());
                var head = p2.X > p1.X ? ArrowDirection.Right : ArrowDirection.Left;
                var (x1, y1) = (x + p1.X + dx1, y + p1.Y);
                var (x2, y2) = (x + p2.X + dx2, y + p2.Y);
                var delta = (p2.X > p1.X ? -1 : 1) * 1.5 * ActivityLayout.HexagonHalfSize;
                if ((p2.X >= p1.X) == (x2 >= x1))
                {
                    var middle = (y1 + y2) / 2;
                    canvas.Add(new Snake(link, ArrowDirection.None).Add(x1, y1).Add(x1, middle).Add(x2 + delta, middle).Add(x2 + delta, y2));
                    canvas.Add(new Snake(link, head).Add(x2 + delta, y2).Add(x2, y2));
                }
                else
                {
                    var yb = y2 - (1.5 * ActivityLayout.HexagonHalfSize);
                    canvas.Add(new Snake(link, ArrowDirection.None).Add(x1, y1).Add(x1, yb).Add(x2 + delta, yb));
                    canvas.Add(new Snake(link, head).Add(x2 + delta, yb).Add(x2 + delta, y2).Add(x2, y2));
                }
            });
    }

    /// <summary>
    ///     Creates the arrow from the only branch that continues to the bottom of the if (PlantUML's
    ///     <c>ConnectionVerticalThenHorizontalDirect</c>).
    /// </summary>
    /// <param name="tile">The branch tile.</param>
    /// <param name="branch">The branch.</param>
    /// <param name="first">Whether it is the then branch.</param>
    /// <returns>The connection.</returns>
    private DelegateConnection OutDirect(Tile tile, ActivityBranch branch, bool first)
    {
        var link = Layout.BranchOutOf(branch);
        (double X, double Y) P1()
        {
            var offset = first ? Branch1Offset : Branch2Offset;
            return (offset.X + tile.Geometry.Left, offset.Y + tile.Geometry.OutY);
        }

        return new DelegateConnection(
            tile,
            _diamond2,
            (canvas, x, y) =>
            {
                var p1 = P1();
                var total = Internal;
                var snake = new Snake(link, ArrowDirection.None) { Emphasize = branch.IsEmpty ? ArrowDirection.Down : ArrowDirection.None };
                canvas.Add(snake.Add(x + p1.X, y + p1.Y).Add(x + p1.X, y + total.Height).Add(x + total.Left, y + total.Height));
            },
            (canvas, x, y, dx1, dx2) =>
            {
                var p1 = P1();
                var total = Internal;
                var (x1, y1) = (x + p1.X + dx1, y + p1.Y);
                var (x2, y2) = (x + total.Left + dx2, y + total.Height - ActivityLayout.HexagonHalfSize);
                canvas.Add(new Snake(link, ArrowDirection.None).Add(x1, y1).Add(x1, y2).Add(x2, y2).Add(x2, y + total.Height));
            });
    }
}
