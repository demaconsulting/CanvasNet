// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;

namespace DemaConsulting.CanvasNet.PlantUml.Activity;

/// <summary>
///     An if whose second branch is empty or only a stop (PlantUML's <c>FtileIfDown</c>): the first branch below the
///     hexagon, and the second either drawn right of the hexagon (a stop) or as an arrow around the first branch to the
///     merge diamond.
/// </summary>
internal sealed class IfDownTile : Tile
{
    /// <summary>The branch below the hexagon.</summary>
    private readonly Tile _then;

    /// <summary>The hexagon.</summary>
    private readonly HexagonTile _diamond1;

    /// <summary>The merge diamond, or the space below.</summary>
    private readonly Tile _diamond2;

    /// <summary>The stop branch drawn right of the hexagon, or null.</summary>
    private readonly Tile? _optionalStop;

    /// <summary>The if.</summary>
    private readonly ActivityIf _block;

    /// <summary>The note of the if, or null.</summary>
    private readonly NoteShape? _note;

    /// <summary>
    ///     Initializes the tile.
    /// </summary>
    /// <param name="layout">The layout.</param>
    /// <param name="block">The if.</param>
    /// <param name="then">The branch below the hexagon.</param>
    /// <param name="diamond1">The hexagon.</param>
    /// <param name="diamond2">The merge diamond.</param>
    /// <param name="optionalStop">The stop branch, or null.</param>
    private IfDownTile(ActivityLayout layout, ActivityIf block, Tile then, HexagonTile diamond1, Tile diamond2, Tile? optionalStop)
        : base(layout)
    {
        _block = block;
        _then = then;
        _diamond1 = diamond1;
        _diamond2 = diamond2;
        _optionalStop = optionalStop;
        if (block.Notes.Count == 1)
        {
            _note = new NoteShape(layout, block.Notes[0]);
        }
    }

    /// <inheritdoc/>
    public override ActivityLane? LaneIn => _block.Lane;

    /// <inheritdoc/>
    public override ActivityLane? LaneOut => _optionalStop is null ? LaneIn : _then.LaneOut;

    /// <inheritdoc/>
    protected override IReadOnlySet<ActivityLane> ComputeLanes()
    {
        var lanes = new HashSet<ActivityLane>(_then.Lanes);
        if (_block.Lane is { } lane)
        {
            lanes.Add(lane);
        }

        return lanes;
    }

    /// <summary>
    ///     Gets the size of the note.
    /// </summary>
    private (double Width, double Height) NoteSize => _note is null ? (0, 0) : (_note.Width, _note.Height);

    /// <summary>
    ///     Gets the space between the hexagon and a stop branch.
    /// </summary>
    private double AdditionalWidth
    {
        get
        {
            var stop = _optionalStop!.Geometry.Width;
            return Math.Max(stop, _diamond1.EastLabelWidth + (stop / 2));
        }
    }

    /// <summary>
    ///     Gets the offset of the branch below the hexagon.
    /// </summary>
    private (double X, double Y) ThenOffset
    {
        get
        {
            var total = Geometry;
            var noteHeight = NoteSize.Height;
            var d1 = _diamond1.Geometry;
            var y = noteHeight + d1.Height + ((total.Height - noteHeight - d1.Height - _diamond2.Geometry.Height - _then.Geometry.Height) / 2);
            return (total.Left - _then.Geometry.Left, y);
        }
    }

    /// <summary>
    ///     Gets the offset of the hexagon.
    /// </summary>
    private (double X, double Y) Diamond1Offset => (Geometry.Left - _diamond1.Geometry.Left, NoteSize.Height);

    /// <summary>
    ///     Gets the offset of the merge diamond.
    /// </summary>
    private (double X, double Y) Diamond2Offset => (Geometry.Left - _diamond2.Geometry.Left, Geometry.Height - _diamond2.Geometry.Height);

    /// <summary>
    ///     Gets the offset of the stop branch.
    /// </summary>
    private (double X, double Y) StopOffset
    {
        get
        {
            var d1 = _diamond1.Geometry;
            var stop = _optionalStop!.Geometry;
            var y = d1.InY + ((d1.Height - d1.InY - stop.Height) / 2);
            return (Geometry.Left - d1.Left + d1.Width + AdditionalWidth, y);
        }
    }

    /// <summary>
    ///     Creates the tile with its arrows.
    /// </summary>
    /// <param name="layout">The layout.</param>
    /// <param name="block">The if.</param>
    /// <param name="diamond1">The hexagon.</param>
    /// <param name="diamond2">The merge diamond.</param>
    /// <param name="then">The branch below the hexagon.</param>
    /// <param name="optionalStop">The stop branch, or null.</param>
    /// <param name="elseColor">The color of the arrow of the other branch.</param>
    /// <returns>The tile.</returns>
    public static Tile Create(ActivityLayout layout, ActivityIf block, HexagonTile diamond1, Tile diamond2, Tile then, Tile? optionalStop, ActivityLink elseColor)
    {
        var tile = new IfDownTile(layout, block, then, diamond1, optionalStop is null ? diamond2 : new EmptyTile(layout, 0, 0, null), optionalStop);
        var connections = new List<TileConnection> { tile.ConnectionIn(layout.Rainbow(then.InLink)) };
        if (optionalStop is null)
        {
            if (!then.Geometry.HasOut)
            {
                connections.Add(tile.ConnectionElse(elseColor, false, true));
            }
            else
            {
                var left = block.Lane is { } lane && IsSmallerThanAll(lane, then.Lanes);
                connections.Add(tile.ConnectionElse(elseColor, left, false));
            }
        }
        else
        {
            connections.Add(tile.ConnectionHorizontal(elseColor));
        }

        connections.Add(tile.ConnectionOut(layout.Rainbow(then.OutLink)));
        return new ConnectedTile(tile, connections);
    }

    /// <summary>
    ///     Decides whether a swimlane is left of every other lane of a set (PlantUML's
    ///     <c>Swimlane.isSmallerThanAllOthers</c>).
    /// </summary>
    /// <param name="lane">The lane.</param>
    /// <param name="others">The lanes.</param>
    /// <returns>True when it is.</returns>
    internal static bool IsSmallerThanAll(ActivityLane lane, IReadOnlySet<ActivityLane> others)
    {
        if (others.Count == 1 && others.Contains(lane))
        {
            return false;
        }

        return others.All(other => other.Index >= lane.Index);
    }

    /// <inheritdoc/>
    public override void Draw(TileCanvas canvas, double x, double y)
    {
        if (_note is { } note)
        {
            note.Draw(canvas, x + Diamond1Offset.X - note.Width, y, null);
        }

        canvas.Tile(_then, x + ThenOffset.X, y + ThenOffset.Y);
        canvas.Tile(_diamond1, x + Diamond1Offset.X, y + Diamond1Offset.Y);
        if (_optionalStop is null)
        {
            canvas.Tile(_diamond2, x + Diamond2Offset.X, y + Diamond2Offset.Y);
        }
        else
        {
            canvas.Tile(_optionalStop, x + StopOffset.X, y + StopOffset.Y);
        }
    }

    /// <inheritdoc/>
    protected override TileGeometry ComputeGeometry()
    {
        var (noteWidth, noteHeight) = NoteSize;
        var d1 = _diamond1.Geometry;
        var geo = d1.AppendBottom(_then.Geometry).AppendBottom(_diamond2.Geometry);
        var height = geo.Height + (3 * ActivityLayout.HexagonHalfSize) + Math.Max(ActivityLayout.HexagonHalfSize, _diamond1.SouthLabelHeight) + noteHeight;
        var supp = noteWidth > geo.Left ? noteWidth - geo.Left : 0;
        var width = supp + geo.Width + ActivityLayout.HexagonHalfSize;
        if (_optionalStop is not null)
        {
            width += _optionalStop.Geometry.Width + AdditionalWidth;
        }

        var left = supp > 0 ? noteWidth + d1.Left : geo.Left;
        var result = new TileGeometry(width, height, left, d1.InY + noteHeight, height);
        return !_then.Geometry.HasOut && _optionalStop is not null ? result.WithoutOut() : result;
    }

    /// <summary>
    ///     Creates the arrow from the hexagon to the branch below it.
    /// </summary>
    /// <param name="link">The color.</param>
    /// <returns>The connection.</returns>
    private DelegateConnection ConnectionIn(ActivityLink link)
    {
        (double X, double Y) P1() => (Diamond1Offset.X + _diamond1.Geometry.Left, Diamond1Offset.Y + _diamond1.Geometry.OutY);
        (double X, double Y) P2() => (ThenOffset.X + _then.Geometry.Left, ThenOffset.Y + _then.Geometry.InY);
        return new DelegateConnection(
            _diamond1,
            _then,
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
    ///     Creates the arrow from the branch below the hexagon to the merge diamond.
    /// </summary>
    /// <param name="link">The color.</param>
    /// <returns>The connection.</returns>
    private DelegateConnection ConnectionOut(ActivityLink link)
    {
        (double X, double Y) P1() => (ThenOffset.X + _then.Geometry.Left, ThenOffset.Y + _then.Geometry.OutY);
        (double X, double Y) P2() => (Diamond2Offset.X + _diamond2.Geometry.Left, Diamond2Offset.Y + _diamond2.Geometry.InY);
        return new DelegateConnection(
            _then,
            _diamond2,
            (canvas, x, y) =>
            {
                if (!_then.Geometry.HasOut)
                {
                    return;
                }

                var (p1, p2) = (P1(), P2());
                canvas.Add(new Snake(link, ArrowDirection.Down).Add(x + p1.X, y + p1.Y).Add(x + p2.X, y + p2.Y));
            },
            (canvas, x, y, dx1, dx2) =>
            {
                if (!_then.Geometry.HasOut)
                {
                    return;
                }

                var (p1, p2) = (P1(), P2());
                var (x1, y1, x2, y2) = (x + p1.X + dx1, y + p1.Y, x + p2.X + dx2, y + p2.Y);
                var middle = (y1 + y2) / 2;
                canvas.Add(new Snake(link, ArrowDirection.Down).Add(x1, y1).Add(x1, middle).Add(x2, middle).Add(x2, y2));
            });
    }

    /// <summary>
    ///     Creates the arrow of the empty branch around the branch below the hexagon (PlantUML's
    ///     <c>ConnectionElse1</c>, <c>ConnectionElse2</c> and <c>ConnectionElseNoDiamond</c>).
    /// </summary>
    /// <param name="link">The color.</param>
    /// <param name="onLeft">Whether the arrow goes around on the left.</param>
    /// <param name="noDiamond">Whether the arrow ends at the bottom of the tile instead of the merge diamond.</param>
    /// <returns>The connection.</returns>
    private DelegateConnection ConnectionElse(ActivityLink link, bool onLeft, bool noDiamond)
    {
        if (onLeft)
        {
            _diamond1.SwapEastWest();
        }

        return new DelegateConnection(_diamond1, _diamond2, (canvas, x, y) =>
        {
            if (!Geometry.HasOut)
            {
                return;
            }

            var d1 = _diamond1.Geometry;
            var d2 = _diamond2.Geometry;
            var y1 = Diamond1Offset.Y + d1.InY + ((d1.OutY - d1.InY) / 2);
            double x1, x2, y2;
            if (noDiamond)
            {
                (x1, x2, y2) = (Diamond1Offset.X + d1.Width, Geometry.Left, Geometry.Height);
            }
            else
            {
                x1 = Diamond1Offset.X + (onLeft ? 0 : d1.Width);
                x2 = Diamond2Offset.X + (onLeft ? 0 : d2.Width);
                y2 = Diamond2Offset.Y + d2.InY + ((d2.OutY - d2.InY) / 2);
            }

            double side;
            ArrowDirection head;
            if (onLeft)
            {
                side = Math.Min(x1 - ActivityLayout.HexagonHalfSize, ThenOffset.X);
                head = ArrowDirection.Right;
            }
            else
            {
                side = Math.Max(x1 + ActivityLayout.HexagonHalfSize, ThenOffset.X + _then.Geometry.Width);
                head = ArrowDirection.Left;
            }

            canvas.Empty(x + x2, y + y2 - ActivityLayout.HexagonHalfSize, 5, ActivityLayout.HexagonHalfSize);
            canvas.Add(new Snake(link, head) { Emphasize = ArrowDirection.Down }.Add(x + x1, y + y1).Add(x + side, y + y1).Add(x + side, y + y2).Add(x + x2, y + y2));
        });
    }

    /// <summary>
    ///     Creates the arrow from the hexagon to the stop branch on its right.
    /// </summary>
    /// <param name="link">The color.</param>
    /// <returns>The connection.</returns>
    private DelegateConnection ConnectionHorizontal(ActivityLink link) =>
        new(_diamond1, _optionalStop, (canvas, x, y) =>
        {
            var d1 = _diamond1.Geometry;
            var p1 = (X: Diamond1Offset.X + d1.Width, Y: Diamond1Offset.Y + ((d1.InY + d1.OutY) / 2));
            var p2 = (X: StopOffset.X, Y: StopOffset.Y + (_optionalStop!.Geometry.Height / 2));
            canvas.Add(new Snake(link, ArrowDirection.Right).Add(x + p1.X, y + p1.Y).Add(x + p2.X, y + p2.Y));
        });
}

/// <summary>
///     An if with elseif branches (PlantUML's <c>FtileIfLongHorizontal</c>): a chain of hexagons from left to right,
///     each with its branch below it, the else branch at the right, and a horizontal line that joins the branches at
///     the bottom.
/// </summary>
internal sealed class IfLongHorizontalTile : Tile
{
    /// <summary>The space between two couples.</summary>
    private const double XSeparation = 20;

    /// <summary>The hexagons, aligned on their out points.</summary>
    private readonly List<Tile> _diamonds;

    /// <summary>The then branches.</summary>
    private readonly List<Tile> _tiles;

    /// <summary>The else branch.</summary>
    private readonly Tile _tile2;

    /// <summary>The couples: each hexagon above its branch, with the space of its incoming label.</summary>
    private readonly List<MarginTile> _couples = [];

    /// <summary>The couples' assemblies.</summary>
    private readonly List<AssemblyTile> _assemblies = [];

    /// <summary>The if.</summary>
    private readonly ActivityIf _block;

    /// <summary>
    ///     Initializes the tile.
    /// </summary>
    /// <param name="layout">The layout.</param>
    /// <param name="block">The if.</param>
    /// <param name="diamonds">The aligned hexagons.</param>
    /// <param name="inLabelSizes">The widths of the incoming labels.</param>
    /// <param name="tiles">The then branches.</param>
    /// <param name="tile2">The else branch.</param>
    private IfLongHorizontalTile(ActivityLayout layout, ActivityIf block, List<Tile> diamonds, List<double> inLabelSizes, List<Tile> tiles, Tile tile2)
        : base(layout)
    {
        _block = block;
        _diamonds = diamonds;
        _tiles = tiles;
        _tile2 = tile2;
        for (var i = 0; i < diamonds.Count; i++)
        {
            var assembly = new AssemblyTile(layout, diamonds[i], tiles[i]);
            _assemblies.Add(assembly);
            _couples.Add(new MarginTile(assembly, inLabelSizes[i], 0));
        }
    }

    /// <inheritdoc/>
    public override ActivityLane? LaneIn => _block.Lane;

    /// <inheritdoc/>
    protected override IReadOnlySet<ActivityLane> ComputeLanes()
    {
        var lanes = new HashSet<ActivityLane>(MergeLanes([.. _couples, _tile2]));
        if (_block.Lane is { } lane)
        {
            lanes.Add(lane);
        }

        return lanes;
    }

    /// <summary>
    ///     Gets the largest y of the out points of the hexagons.
    /// </summary>
    private double MaxOutY => _diamonds.Max(d => d.Geometry.OutY);

    /// <summary>
    ///     Gets the size of the whole tile.
    /// </summary>
    private (double Width, double Height) Internal
    {
        get
        {
            var width = _couples.Sum(c => c.Geometry.Width) + _tile2.Geometry.Width + (XSeparation * _couples.Count);
            var diamondsHeight = _diamonds.Max(d => d.Geometry.Height);
            var height = Math.Max(_couples.Max(c => c.Geometry.Height), _tile2.Geometry.Height + (diamondsHeight / 2));
            return (width, height + Math.Max(100, MaxOutY));
        }
    }

    /// <summary>
    ///     Creates the tile of an if with elseif branches, with its arrows.
    /// </summary>
    /// <param name="layout">The layout.</param>
    /// <param name="block">The if.</param>
    /// <param name="thens">The then branches and their tiles.</param>
    /// <param name="other">The else branch and its tile.</param>
    /// <param name="fill">The fill of the hexagons.</param>
    /// <returns>The tile.</returns>
    public static Tile Create(ActivityLayout layout, ActivityIf block, List<(ActivityBranch Branch, Tile Tile)> thens, (ActivityBranch Branch, Tile Tile) other, Canvas.Rgba32? fill)
    {
        var tiles = thens.Select(t => (Tile)new MinWidthTile(t.Tile, 30)).ToList();
        var tile2 = new MinWidthTile(other.Tile, 30);
        var hexagons = new List<HexagonTile>();
        var inLabelSizes = new List<double>();
        foreach (var (branch, tile) in thens)
        {
            var test = branch.Test.Length == 0 ? TextBlock.Empty : layout.Text(branch.Test, SkinElement.ActivityDiamond, block.Location);
            var inLabel = layout.Label(branch.InLabel);
            inLabelSizes.Add(inLabel?.Width ?? 0);
            hexagons.Add(new HexagonTile(layout, test, branch.Color ?? fill, block.Lane)
            {
                West = inLabel,
                North = layout.PositiveLabelOf(branch, tile),
                ElseIfGeometry = true,
                Location = block.Location,
            });
        }

        hexagons[^1].East = layout.PositiveLabelOf(other.Branch, other.Tile);

        // The hexagons are aligned on their out points, with 20 pixels below them
        var maxOutY = hexagons.Max(h => h.Geometry.OutY);
        var diamonds = hexagons.Select(h => (Tile)new VerticalMarginTile(h, (maxOutY - h.Geometry.OutY) / 2, 20)).ToList();
        var result = new IfLongHorizontalTile(layout, block, diamonds, inLabelSizes, tiles, tile2);
        var connections = new List<TileConnection>();
        var nbOut = 0;
        for (var i = 0; i < thens.Count; i++)
        {
            var (branch, tile) = thens[i];
            if (tile.Geometry.HasOut)
            {
                nbOut++;
            }

            connections.Add(result.VerticalIn(i, layout.BranchInColorOf(branch, tile)));
            connections.Add(result.VerticalOut(tiles[i], i, layout.BranchOutOf(branch), layout.ArrowText(branch.Special?.Label, block.Location)));
        }

        for (var i = 0; i + 1 < diamonds.Count; i++)
        {
            connections.Add(result.Horizontal(i, layout.Rainbow(thens[i + 1].Branch.InLabel)));
        }

        connections.Add(result.In(layout.Rainbow(block.InLink)));
        connections.Add(result.LastElseIn(layout.BranchInColorOf(other.Branch, other.Tile)));
        connections.Add(result.LastElseOut(layout.BranchOutOf(other.Branch), layout.ArrowText(other.Branch.Special?.Label, block.Location), nbOut));
        if (nbOut > 0)
        {
            connections.Add(result.HorizontalLine(layout.Rainbow(block.OutLink)));
        }

        return new ConnectedTile(result, connections);
    }

    /// <inheritdoc/>
    public override void Draw(TileCanvas canvas, double x, double y)
    {
        for (var i = 0; i < _couples.Count; i++)
        {
            var offset = CoupleOffset(i);
            canvas.Tile(_couples[i], x + offset.X, y + offset.Y);
        }

        var tile2 = Tile2Offset;
        canvas.Tile(_tile2, x + tile2.X, y + tile2.Y);
    }

    /// <inheritdoc/>
    protected override TileGeometry ComputeGeometry()
    {
        var (width, height) = Internal;
        var hasOut = _tiles.Append(_tile2).Any(t => t.Geometry.HasOut);
        return hasOut ? new TileGeometry(width, height, width / 2, 0, height) : TileGeometry.NoOut(width, height, width / 2, 0);
    }

    /// <summary>
    ///     Gets the offset of a couple.
    /// </summary>
    /// <param name="index">The index of the couple.</param>
    /// <returns>The offset.</returns>
    private (double X, double Y) CoupleOffset(int index)
    {
        double x = 0;
        for (var i = 0; i < index; i++)
        {
            x += _couples[i].Geometry.Width + XSeparation;
        }

        return (x, 25);
    }

    /// <summary>
    ///     Gets the offset of the else branch.
    /// </summary>
    private (double X, double Y) Tile2Offset
    {
        get
        {
            var (width, height) = Internal;
            return (width - _tile2.Geometry.Width, (height - _tile2.Geometry.Height) / 2);
        }
    }

    /// <summary>
    ///     Gets the offset of a hexagon.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <returns>The offset.</returns>
    private (double X, double Y) DiamondOffset(int index)
    {
        var couple = CoupleOffset(index);
        var inner = _assemblies[index].Offset1;
        return (couple.X + _couples[index].Margin1 + inner.X, couple.Y + inner.Y);
    }

    /// <summary>
    ///     Gets the offset of a then branch.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <returns>The offset.</returns>
    private (double X, double Y) BranchOffset(int index)
    {
        var couple = CoupleOffset(index);
        var inner = _assemblies[index].Offset2;
        return (couple.X + _couples[index].Margin1 + inner.X, couple.Y + inner.Y);
    }

    /// <summary>
    ///     Gets the y of the middle of a hexagon (where the horizontal arrows leave it).
    /// </summary>
    /// <param name="diamond">The hexagon.</param>
    /// <returns>The y.</returns>
    private static double MiddleY(Tile diamond) => (diamond.Geometry.InY + diamond.Geometry.OutY) / 2;

    /// <summary>
    ///     Creates the arrow from a hexagon to its branch (PlantUML's <c>ConnectionVerticalIn</c>). When the branch
    ///     starts in another swimlane, the crossing pass draws it with a short step below the hexagon.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <param name="link">The color.</param>
    /// <returns>The connection.</returns>
    private DelegateConnection VerticalIn(int index, ActivityLink link)
    {
        (double X, double Y) P1()
        {
            var d = DiamondOffset(index);
            var diamond = _diamonds[index].Geometry;
            return (d.X + diamond.Left, d.Y + diamond.OutY);
        }

        (double X, double Y) P2()
        {
            var b = BranchOffset(index);
            var tile = _tiles[index].Geometry;
            return (b.X + tile.Left, b.Y + tile.InY);
        }

        return new DelegateConnection(
            _diamonds[index],
            _tiles[index],
            (canvas, x, y) =>
            {
                var (p1, p2) = (P1(), P2());
                canvas.Add(new Snake(link, ArrowDirection.Down).Add(x + p1.X, y + p1.Y).Add(x + p2.X, y + p2.Y));
            },
            (canvas, x, y, dx1, dx2) =>
            {
                var (p1, p2) = (P1(), P2());
                var (x1, y1) = (x + p1.X + dx1, y + p1.Y);
                var (x2, y2) = (x + p2.X + dx2, y + p2.Y);
                var middle = y1 + 4;
                canvas.Add(new Snake(link, ArrowDirection.Down).Add(x1, y1).Add(x1, middle).Add(x2, middle).Add(x2, y2));
            });
    }

    /// <summary>
    ///     Creates the arrow from a then branch down to the bottom of the tile.
    /// </summary>
    /// <param name="tile">The branch.</param>
    /// <param name="index">The index.</param>
    /// <param name="link">The color.</param>
    /// <param name="label">The label, or null.</param>
    /// <returns>The connection.</returns>
    private DelegateConnection VerticalOut(Tile tile, int index, ActivityLink link, TextBlock? label) =>
        new(tile, null, (canvas, x, y) =>
        {
            if (!tile.Geometry.HasOut)
            {
                return;
            }

            var b = BranchOffset(index);
            var p1 = (X: b.X + tile.Geometry.Left, Y: b.Y + tile.Geometry.OutY);
            canvas.Add(new Snake(link, ArrowDirection.Down) { Label = label }.Add(x + p1.X, y + p1.Y).Add(x + p1.X, y + Internal.Height));
        });

    /// <summary>
    ///     Creates the arrow from a hexagon to the next one.
    /// </summary>
    /// <param name="index">The index of the first hexagon.</param>
    /// <param name="link">The color.</param>
    /// <returns>The connection.</returns>
    private DelegateConnection Horizontal(int index, ActivityLink link) =>
        new(_diamonds[index], _diamonds[index + 1], (canvas, x, y) =>
        {
            var (d1, d2) = (DiamondOffset(index), DiamondOffset(index + 1));
            var g1 = _diamonds[index].Geometry;
            canvas.Add(new Snake(link, ArrowDirection.Right).Add(x + d1.X + (g1.Left * 2), y + d1.Y + MiddleY(_diamonds[index])).Add(x + d2.X, y + d2.Y + MiddleY(_diamonds[index + 1])));
        });

    /// <summary>
    ///     Creates the arrow from the top of the tile to the first hexagon.
    /// </summary>
    /// <param name="link">The color.</param>
    /// <returns>The connection.</returns>
    private DelegateConnection In(ActivityLink link) =>
        new(null, _diamonds[0], (canvas, x, y) =>
        {
            var d = DiamondOffset(0);
            var p2 = (X: d.X + _diamonds[0].Geometry.Left, Y: d.Y + _diamonds[0].Geometry.InY);
            var p1 = (X: Internal.Width / 2, Y: 0.0);
            canvas.Add(new Snake(link, ArrowDirection.Down).Add(x + p1.X, y + p1.Y).Add(x + p2.X, y + p1.Y).Add(x + p2.X, y + p2.Y));
        });

    /// <summary>
    ///     Creates the arrow from the last hexagon to the else branch.
    /// </summary>
    /// <param name="link">The color.</param>
    /// <returns>The connection.</returns>
    private DelegateConnection LastElseIn(ActivityLink link) =>
        new(_diamonds[^1], _tile2, (canvas, x, y) =>
        {
            var index = _diamonds.Count - 1;
            var d = DiamondOffset(index);
            var g = _diamonds[index].Geometry;
            var p1 = (X: d.X + (g.Left * 2), Y: d.Y + MiddleY(_diamonds[index]));
            var t = Tile2Offset;
            var p2 = (X: t.X + _tile2.Geometry.Left, Y: t.Y + _tile2.Geometry.InY);
            canvas.Add(new Snake(link, ArrowDirection.Down).Add(x + p1.X, y + p1.Y).Add(x + p2.X, y + p1.Y).Add(x + p2.X, y + p2.Y));
        });

    /// <summary>
    ///     Creates the arrow from the else branch down to the bottom of the tile.
    /// </summary>
    /// <param name="link">The color.</param>
    /// <param name="label">The label, or null.</param>
    /// <param name="nbOut">The number of then branches that continue.</param>
    /// <returns>The connection.</returns>
    private DelegateConnection LastElseOut(ActivityLink link, TextBlock? label, int nbOut) =>
        new(_tile2, null, (canvas, x, y) =>
        {
            if (!_tile2.Geometry.HasOut)
            {
                return;
            }

            var t = Tile2Offset;
            var p1 = (X: t.X + _tile2.Geometry.Left, Y: t.Y + _tile2.Geometry.OutY);
            var (width, height) = Internal;
            var snake = new Snake(link, ArrowDirection.Down) { Label = label }.Add(x + p1.X, y + p1.Y).Add(x + p1.X, y + height);
            if (nbOut == 0)
            {
                snake.Add(x + (width / 2), y + height);
            }

            canvas.Add(snake);
        });

    /// <summary>
    ///     Creates the horizontal line that joins the branches at the bottom (PlantUML's <c>ConnectionHline</c>). With
    ///     swimlanes, each lane draws only its part of the line, and only the lanes from the first to the last one
    ///     where a branch ends draw it.
    /// </summary>
    /// <param name="link">The color.</param>
    /// <returns>The connection.</returns>
    private DelegateConnection HorizontalLine(ActivityLink link) =>
        new(null, null, (canvas, x, y) =>
        {
            if (!Geometry.HasOut)
            {
                return;
            }

            var (width, height) = Internal;
            var ends = canvas.Mode == CanvasMode.Lane ? LaneMinMax(canvas.Layout.Lanes, canvas.Lane!, width) : SimpleMinMax(width);
            if (ends is not (var minX, var maxX))
            {
                return;
            }

            canvas.Add(new Snake(link, ArrowDirection.None) { Merge = SnakeMerge.None }.Add(x + minX, y + height).Add(x + maxX, y + height));
        });

    /// <summary>
    ///     Gets the ends of the joining line drawn without swimlanes (PlantUML's <c>getMinmaxSimple</c>).
    /// </summary>
    /// <param name="width">The width of the tile.</param>
    /// <returns>The smallest and the largest x.</returns>
    private (double MinX, double MaxX) SimpleMinMax(double width)
    {
        var minX = Math.Min(width / 2, Geometry.Left);
        var maxX = Math.Max(width / 2, Geometry.Left);
        foreach (var outX in OutXs(null))
        {
            (minX, maxX) = (Math.Min(minX, outX), Math.Max(maxX, outX));
        }

        return (minX, maxX);
    }

    /// <summary>
    ///     Gets the ends of the part of the joining line drawn in one swimlane (PlantUML's <c>getMinmax</c>): nothing
    ///     outside the lanes from the first to the last one where a branch ends, the whole width in the lanes between
    ///     them, and from or to the branches that end in the first and the last lane.
    /// </summary>
    /// <param name="lanes">The swimlanes, from left to right.</param>
    /// <param name="lane">The swimlane drawn.</param>
    /// <param name="width">The width of the tile.</param>
    /// <returns>The smallest and the largest x, or null when the lane draws no part of the line.</returns>
    private (double MinX, double MaxX)? LaneMinMax(IReadOnlyList<ActivityLane> lanes, ActivityLane lane, double width)
    {
        var all = new List<Tile>(_couples) { _tile2 };
        var current = IndexOf(lanes, lane);
        var first = -1;
        var last = -1;
        for (var i = 0; i < lanes.Count; i++)
        {
            if (all.Any(t => OutcomeIn(t, lanes[i])))
            {
                first = first < 0 ? i : first;
                last = i;
            }
        }

        if (current < 0 || first < 0 || current < first || current > last)
        {
            return null;
        }

        var minX = Math.Min(current != first ? 0 : width, Geometry.Left);
        var maxX = Math.Max(current != last ? width : 0, Geometry.Left);
        foreach (var outX in OutXs(lane))
        {
            (minX, maxX) = (Math.Min(minX, outX), Math.Max(maxX, outX));
        }

        return (minX, maxX);
    }

    /// <summary>
    ///     Gets the x of the out points of the branches that continue, in the coordinates of the tile.
    /// </summary>
    /// <param name="lane">The swimlane the branches must end in, or null for every branch.</param>
    /// <returns>The x values.</returns>
    private IEnumerable<double> OutXs(ActivityLane? lane)
    {
        for (var i = 0; i < _couples.Count; i++)
        {
            if (_couples[i].Geometry.HasOut && (lane is null || OutcomeIn(_couples[i], lane)))
            {
                yield return CoupleOffset(i).X + _couples[i].Geometry.Left;
            }
        }

        if (_tile2.Geometry.HasOut && (lane is null || OutcomeIn(_tile2, lane)))
        {
            yield return Tile2Offset.X + _tile2.Geometry.Left;
        }
    }

    /// <summary>
    ///     Decides whether a branch continues in a swimlane (PlantUML's <c>ftileDoesOutcomeInThatSwimlane</c>, with
    ///     the out point that <c>atLeastOne</c> also asks for).
    /// </summary>
    /// <param name="tile">The branch.</param>
    /// <param name="lane">The swimlane.</param>
    /// <returns>True when the branch has an out point in that lane.</returns>
    private static bool OutcomeIn(Tile tile, ActivityLane lane) =>
        tile.Geometry.HasOut && tile.LaneOut == lane && tile.Lanes.Contains(lane);

    /// <summary>
    ///     Finds the index of a swimlane.
    /// </summary>
    /// <param name="lanes">The swimlanes.</param>
    /// <param name="lane">The swimlane.</param>
    /// <returns>The index, or -1.</returns>
    private static int IndexOf(IReadOnlyList<ActivityLane> lanes, ActivityLane lane)
    {
        for (var i = 0; i < lanes.Count; i++)
        {
            if (lanes[i] == lane)
            {
                return i;
            }
        }

        return -1;
    }
}
