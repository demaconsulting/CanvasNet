// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;

namespace DemaConsulting.CanvasNet.PlantUml.Activity;

/// <content>
///     The composition of the instruction tree into tiles (PlantUML's <c>createFtile</c> methods and its chain of
///     tile factories).
/// </content>
internal sealed partial class ActivityLayout
{
    /// <summary>
    ///     Builds the tile of a list: its instructions stacked with the arrows between them.
    /// </summary>
    /// <param name="list">The list.</param>
    /// <param name="isRoot">Whether the list is the root of the diagram.</param>
    /// <returns>The tile.</returns>
    private Tile BuildList(ActivityList list, bool isRoot = false)
    {
        Tick();
        if (list.Items.Count == 0)
        {
            Tile empty = new EmptyTile(this, 0, 0, list.Lane);

            // As PlantUML does, the out link of an empty list decorates its in link
            return list.OutLink is { } link ? new DecoratedTile(empty, inLink: link) : empty;
        }

        // A note written before the first instruction is a note of its own
        Tile? result = list.Notes.Count > 0 ? new NoteAloneTile(this, list.Notes[0], list.Lane) : null;
        for (var i = 0; i < list.Items.Count; i++)
        {
            var instruction = list.Items[i];
            var current = Build(instruction, isRoot && i == list.Items.Count - 1);
            if (!instruction.InLink.IsNone)
            {
                current = new DecoratedTile(current, inLink: instruction.InLink);
            }

            result = result is null ? current : Assembly(result, current);
        }

        return list.OutLink is { } outLink ? new DecoratedTile(result!, outLink: outLink) : result!;
    }

    /// <summary>
    ///     Builds the tile of one instruction.
    /// </summary>
    /// <param name="instruction">The instruction.</param>
    /// <param name="isLastOfRoot">Whether it is the last instruction of the diagram.</param>
    /// <returns>The tile.</returns>
    private Tile Build(ActivityInstruction instruction, bool isLastOfRoot)
    {
        Tick();
        switch (instruction)
        {
            case ActivityAction action:
                var box = WithNotes(new BoxTile(this, action.Text, action.Fill, action.Lane, action.InLink, action.Location), action);
                return action.Killed ? new KilledTile(box) : box;
            case ActivityStart start:
                return WithNotes(new CircleTile(this, CircleKind.Start, start.Lane, start.InLink, start.Location), start);
            case ActivityStop stop:
                return WithNotes(new CircleTile(this, CircleKind.Stop, stop.Lane, stop.InLink, stop.Location), stop);
            case ActivityEnd end:
                return WithNotes(new CircleTile(this, CircleKind.End, end.Lane, end.InLink, end.Location), end);
            case ActivitySpot spot:
                var circle = WithNotes(new CircleTile(this, CircleKind.Spot, spot.Lane, spot.InLink, spot.Location) { Spot = spot.Text, SpotFill = spot.Fill }, spot);
                return spot.Killed ? new KilledTile(circle) : circle;
            case ActivityIf branches:
                return BuildIf(branches);
            case ActivityWhile loop:
                return BuildWhile(loop);
            case ActivityRepeat repeat:
                return BuildRepeat(repeat, isLastOfRoot);
            case ActivityFork fork:
                var parallel = BuildFork(fork);
                return fork.Notes.Count > 0 ? NotesOf(parallel, fork.Notes, false) : parallel;
            case ActivitySplit split:
                return BuildSplit(split);
            case ActivityGroup group:
                return BuildGroup(group);
            default:
                return BuildList((ActivityList)instruction);
        }
    }

    /// <summary>
    ///     Adds the notes of an instruction to its tile (PlantUML's <c>eventuallyAddNote</c>).
    /// </summary>
    /// <param name="tile">The tile.</param>
    /// <param name="instruction">The instruction.</param>
    /// <returns>The tile with its notes.</returns>
    private static Tile WithNotes(Tile tile, ActivityInstruction instruction) =>
        instruction.Notes.Count == 0 ? tile : NotesOf(tile, instruction.Notes, true);

    /// <summary>
    ///     Adds notes to a tile: one note with a pointer, or several stacked without (PlantUML's
    ///     <c>FtileWithNoteOpale.create</c>).
    /// </summary>
    /// <param name="tile">The tile.</param>
    /// <param name="notes">The notes.</param>
    /// <param name="withLink">Whether a single note points to the tile.</param>
    /// <returns>The tile with its notes.</returns>
    private static Tile NotesOf(Tile tile, IReadOnlyList<ActivityNote> notes, bool withLink) =>
        notes.Count > 1 ? new NotesTile(tile, notes) : new NoteTile(tile, notes[0], withLink);

    /// <summary>
    ///     Builds the tile of a group: its instructions in a frame.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <returns>The tile.</returns>
    private Tile BuildGroup(ActivityGroup group)
    {
        var inner = BuildList(group.Body);
        if (group.Notes.Count > 0)
        {
            inner = new NotesTile(inner, [group.Notes[0]]);
        }

        return group.Framed ? new GroupTile(inner, group) : inner;
    }

    /// <summary>
    ///     Stacks two tiles with the arrow between them (PlantUML's <c>FtileFactoryDelegatorAssembly</c>): 35 pixels
    ///     apart plus the height of the arrow's label, the arrow drawn from the out point of the first to the in point
    ///     of the second, and the stack widened when the label reaches beyond it.
    /// </summary>
    /// <param name="tile1">The upper tile.</param>
    /// <param name="tile2">The lower tile.</param>
    /// <returns>The stack.</returns>
    private Tile Assembly(Tile tile1, Tile tile2)
    {
        var label = Label(tile2.InLink);
        var height = AssemblySpace + (label?.Height ?? 0);
        var spaced = new VerticalMarginTile(tile1, 0, height);
        var assembly = new AssemblyTile(this, spaced, tile2);
        if (!tile1.Geometry.HasOut)
        {
            return assembly;
        }

        var p1 = (X: assembly.Offset1.X + tile1.Geometry.Left, Y: assembly.Offset1.Y + tile1.Geometry.OutY);
        var p2 = (X: assembly.Offset2.X + tile2.Geometry.Left, Y: assembly.Offset2.Y + tile2.Geometry.InY);
        var link = Rainbow(tile2.InLink);
        Snake Create() => new(link, ArrowDirection.Down) { Label = label };
        var connection = new DelegateConnection(
            tile1,
            tile2,
            (canvas, x, y) => canvas.Add(Create().Add(x + p1.X, y + p1.Y).Add(x + p2.X, y + p2.Y)),
            (canvas, x, y, dx1, dx2) =>
            {
                var (x1, y1) = (x + p1.X + dx1, y + p1.Y);
                var (x2, y2) = (x + p2.X + dx2, y + p2.Y);
                var middle = (y1 + y2) / 2;
                canvas.Add(Create().Add(x1, y1).Add(x1, middle).Add(x2, middle).Add(x2, y2));
            });
        Tile result = new ConnectedTile(assembly, [connection]);
        if (label is not null)
        {
            var maxX = Create().Add(p1.X, p1.Y).Add(p2.X, p2.Y).MaxX();
            if (result.Geometry.Width < maxX)
            {
                result = new RightMarginTile(result, maxX);
            }
        }

        return result;
    }

    /// <summary>
    ///     Gets the label of the arrow into a branch: the label of its first arrow, or the branch's label (PlantUML's
    ///     <c>Branch.getDisplayPositive</c>).
    /// </summary>
    /// <param name="branch">The branch.</param>
    /// <param name="tile">The tile of the branch.</param>
    /// <returns>The label, or null.</returns>
    internal TextBlock? PositiveLabelOf(ActivityBranch branch, Tile tile) =>
        tile.InLink.Label is { Length: > 0 } ? Label(tile.InLink) : Label(branch.LabelPositive);

    /// <summary>
    ///     Gets the color of the arrow into a branch (PlantUML's <c>Branch.getInColor</c>).
    /// </summary>
    /// <param name="branch">The branch.</param>
    /// <param name="tile">The tile of the branch.</param>
    /// <returns>The arrow.</returns>
    internal ActivityLink BranchInColorOf(ActivityBranch branch, Tile tile)
    {
        if (branch.IsEmpty)
        {
            return Rainbow(tile.OutLink);
        }

        return branch.LabelPositive.HasRainbow ? Rainbow(branch.LabelPositive) : Rainbow(tile.InLink);
    }

    /// <summary>
    ///     Gets the color of the arrow out of a branch (PlantUML's <c>Branch.getOut</c>).
    /// </summary>
    /// <param name="branch">The branch.</param>
    /// <returns>The arrow.</returns>
    internal ActivityLink BranchOutOf(ActivityBranch branch) => Rainbow(branch.Special ?? branch.InLinkRendering);

    /// <summary>
    ///     Gets the fill of the diamonds of the theme.
    /// </summary>
    private Canvas.Rgba32? DiamondFill => Theme[SkinElement.ActivityDiamond].Fill;
}
