// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.PlantUml.Activity;

/// <summary>
///     How an arrow of an activity diagram is stroked.
/// </summary>
internal enum ActivityLinkStyle
{
    /// <summary>A solid line one pixel wide.</summary>
    Normal,

    /// <summary>A dashed line (<c>-[dashed]-&gt;</c>).</summary>
    Dashed,

    /// <summary>A dotted line (<c>-[dotted]-&gt;</c>).</summary>
    Dotted,

    /// <summary>A bold line (<c>-[bold]-&gt;</c>).</summary>
    Bold,

    /// <summary>An arrow that is not drawn (<c>-[hidden]-&gt;</c>).</summary>
    Hidden,
}

/// <summary>
///     The rendering of an arrow of an activity diagram: its label, its color and its style (PlantUML's
///     <c>LinkRendering</c>).
/// </summary>
/// <param name="Label">The label, or null for none.</param>
/// <param name="Color">The color, or null for the theme's arrow color.</param>
/// <param name="Style">The stroke style.</param>
/// <param name="Location">The location of the arrow statement (unknown for an arrow without one).</param>
internal sealed record ActivityLink(string? Label, Rgba32? Color, ActivityLinkStyle Style, PlantUmlLocation Location)
{
    /// <summary>
    ///     Gets the rendering of an arrow without label, color or style.
    /// </summary>
    public static ActivityLink None { get; } = new(null, null, ActivityLinkStyle.Normal, default);

    /// <summary>
    ///     Gets a value indicating whether the arrow has neither a label, a color nor a style.
    /// </summary>
    public bool IsNone => Label is null && Color is null && Style == ActivityLinkStyle.Normal;

    /// <summary>
    ///     Gets a value indicating whether the arrow has a color or a style (PlantUML's non-empty rainbow).
    /// </summary>
    public bool HasRainbow => Color is not null || Style != ActivityLinkStyle.Normal;
}

/// <summary>
///     A swimlane of an activity diagram.
/// </summary>
internal sealed class ActivityLane
{
    /// <summary>
    ///     Initializes a swimlane.
    /// </summary>
    /// <param name="name">The name that identifies it.</param>
    /// <param name="index">Its order of declaration (from the left).</param>
    /// <param name="location">The location of its first declaration.</param>
    public ActivityLane(string name, int index, PlantUmlLocation location)
    {
        Name = name;
        Display = name;
        Index = index;
        Location = location;
    }

    /// <summary>
    ///     Gets the name that identifies the swimlane.
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///     Gets or sets the title drawn at the top of the swimlane (its name unless a label is given).
    /// </summary>
    public string Display { get; set; }

    /// <summary>
    ///     Gets the order of declaration, from the left.
    /// </summary>
    public int Index { get; }

    /// <summary>
    ///     Gets or sets the fill of the swimlane, or null for none.
    /// </summary>
    public Rgba32? Fill { get; set; }

    /// <summary>
    ///     Gets the location of the first declaration.
    /// </summary>
    public PlantUmlLocation Location { get; }
}

/// <summary>
///     Where a note is drawn next to the instruction it belongs to.
/// </summary>
internal enum ActivityNotePosition
{
    /// <summary>On the left (the default).</summary>
    Left,

    /// <summary>On the right.</summary>
    Right,
}

/// <summary>
///     A note of an activity diagram.
/// </summary>
/// <param name="Text">The text (creole markup and line feeds allowed).</param>
/// <param name="Position">The side it is drawn on.</param>
/// <param name="Floating">Whether it is a floating note (drawn without a link to its instruction).</param>
/// <param name="Fill">The fill, or null for the theme's.</param>
/// <param name="Lane">The swimlane the note was written in, or null.</param>
/// <param name="Location">The location of the note statement.</param>
internal sealed record ActivityNote(string Text, ActivityNotePosition Position, bool Floating, Rgba32? Fill, ActivityLane? Lane, PlantUmlLocation Location);

/// <summary>
///     An instruction of an activity diagram: an element of the flow, with the arrow that leads into it and its notes
///     (PlantUML's <c>Instruction</c>).
/// </summary>
/// <remarks>
///     <para>
///         The instructions form a tree: lists hold instructions in order, and the blocks (if, while, repeat, fork,
///         split, groups) hold lists. The parser builds the tree as it reads the statements (an instruction is changed
///         only while it is open: a note or a <c>kill</c> attaches to the last instruction); the finished tree is not
///         changed again and is read by <see cref="ActivityLayout"/>.
///     </para>
/// </remarks>
internal abstract class ActivityInstruction
{
    /// <summary>The notes of the instruction.</summary>
    private readonly List<ActivityNote> _notes = [];

    /// <summary>
    ///     Initializes an instruction.
    /// </summary>
    /// <param name="lane">The swimlane it starts in, or null.</param>
    /// <param name="inLink">The arrow that leads into it.</param>
    /// <param name="location">The location of its statement.</param>
    protected ActivityInstruction(ActivityLane? lane, ActivityLink inLink, PlantUmlLocation location)
    {
        Lane = lane;
        OwnInLink = inLink;
        Location = location;
    }

    /// <summary>
    ///     Gets the swimlane the instruction starts in, or null.
    /// </summary>
    public ActivityLane? Lane { get; }

    /// <summary>
    ///     Gets the arrow that leads into the instruction.
    /// </summary>
    public virtual ActivityLink InLink => OwnInLink;

    /// <summary>
    ///     Gets the location of the statement.
    /// </summary>
    public PlantUmlLocation Location { get; }

    /// <summary>
    ///     Gets the notes of the instruction.
    /// </summary>
    public IReadOnlyList<ActivityNote> Notes => _notes;

    /// <summary>
    ///     Gets the swimlanes the instruction draws in.
    /// </summary>
    public virtual IEnumerable<ActivityLane> Lanes => Lane is null ? [] : [Lane];

    /// <summary>
    ///     Gets the arrow given to the constructor.
    /// </summary>
    protected ActivityLink OwnInLink { get; }

    /// <summary>
    ///     Attaches a note to the instruction (or to the last instruction inside it).
    /// </summary>
    /// <param name="note">The note.</param>
    /// <returns>True when the note was attached.</returns>
    public virtual bool AddNote(ActivityNote note)
    {
        _notes.Add(note);
        return true;
    }

    /// <summary>
    ///     Ends the flow after the instruction (<c>kill</c> or <c>detach</c>).
    /// </summary>
    /// <returns>True when the flow can end there.</returns>
    public virtual bool Kill() => false;
}

/// <summary>
///     A list of instructions drawn one below the other (PlantUML's <c>InstructionList</c>).
/// </summary>
/// <param name="lane">The swimlane of an empty list, or null.</param>
internal sealed class ActivityList(ActivityLane? lane) : ActivityInstruction(lane, ActivityLink.None, default)
{
    /// <summary>The instructions in order.</summary>
    private readonly List<ActivityInstruction> _items = [];

    /// <summary>
    ///     Gets the instructions in order.
    /// </summary>
    public IReadOnlyList<ActivityInstruction> Items => _items;

    /// <summary>
    ///     Gets the last instruction, or null for an empty list.
    /// </summary>
    public ActivityInstruction? Last => _items.Count == 0 ? null : _items[^1];

    /// <summary>
    ///     Gets or sets the arrow that leaves the list (set by the statement that ends a branch).
    /// </summary>
    public ActivityLink? OutLink { get; set; }

    /// <inheritdoc/>
    public override ActivityLink InLink => _items.Count == 0 ? ActivityLink.None : _items[0].InLink;

    /// <inheritdoc/>
    public override IEnumerable<ActivityLane> Lanes => _items.SelectMany(i => i.Lanes).Distinct();

    /// <summary>
    ///     Gets a value indicating whether the list is only one stop, end, spot, or killed action (PlantUML draws such
    ///     a branch of an if beside the diamond).
    /// </summary>
    public bool IsOnlySingleStopOrSpot => _items.Count == 1 && _items[0] switch
    {
        ActivityAction action => action.Killed,
        ActivitySpot => true,
        ActivityStop stop => stop.Notes.Count == 0,
        ActivityEnd end => end.Notes.Count == 0,
        _ => false,
    };

    /// <summary>
    ///     Appends an instruction.
    /// </summary>
    /// <param name="instruction">The instruction.</param>
    public void Add(ActivityInstruction instruction) => _items.Add(instruction);

    /// <inheritdoc/>
    public override bool AddNote(ActivityNote note) => Last is { } last ? last.AddNote(note) : base.AddNote(note);

    /// <inheritdoc/>
    public override bool Kill() => Last is { } last && last.Kill();
}

/// <summary>
///     An action <c>:text;</c>, drawn as a box with rounded corners (PlantUML's <c>InstructionSimple</c>).
/// </summary>
/// <param name="text">The text.</param>
/// <param name="fill">The fill, or null for the theme's.</param>
/// <param name="lane">The swimlane.</param>
/// <param name="inLink">The arrow that leads into it.</param>
/// <param name="location">The location.</param>
internal sealed class ActivityAction(string text, Rgba32? fill, ActivityLane? lane, ActivityLink inLink, PlantUmlLocation location)
    : ActivityInstruction(lane, inLink, location)
{
    /// <summary>
    ///     Gets the text.
    /// </summary>
    public string Text { get; } = text;

    /// <summary>
    ///     Gets the fill, or null for the theme's.
    /// </summary>
    public Rgba32? Fill { get; } = fill;

    /// <summary>
    ///     Gets a value indicating whether the flow ends after the action.
    /// </summary>
    public bool Killed { get; private set; }

    /// <inheritdoc/>
    public override bool Kill()
    {
        Killed = true;
        return true;
    }
}

/// <summary>
///     The start node <c>start</c>.
/// </summary>
/// <param name="lane">The swimlane.</param>
/// <param name="inLink">The arrow that leads into it.</param>
/// <param name="location">The location.</param>
internal sealed class ActivityStart(ActivityLane? lane, ActivityLink inLink, PlantUmlLocation location) : ActivityInstruction(lane, inLink, location);

/// <summary>
///     The stop node <c>stop</c>.
/// </summary>
/// <param name="lane">The swimlane.</param>
/// <param name="inLink">The arrow that leads into it.</param>
/// <param name="location">The location.</param>
internal sealed class ActivityStop(ActivityLane? lane, ActivityLink inLink, PlantUmlLocation location) : ActivityInstruction(lane, inLink, location);

/// <summary>
///     The end node <c>end</c> (a circle with a cross).
/// </summary>
/// <param name="lane">The swimlane.</param>
/// <param name="inLink">The arrow that leads into it.</param>
/// <param name="location">The location.</param>
internal sealed class ActivityEnd(ActivityLane? lane, ActivityLink inLink, PlantUmlLocation location) : ActivityInstruction(lane, inLink, location);

/// <summary>
///     A connector <c>(A)</c>: a circle with one character.
/// </summary>
/// <param name="text">The character.</param>
/// <param name="fill">The fill, or null for the theme's.</param>
/// <param name="lane">The swimlane.</param>
/// <param name="inLink">The arrow that leads into it.</param>
/// <param name="location">The location.</param>
internal sealed class ActivitySpot(string text, Rgba32? fill, ActivityLane? lane, ActivityLink inLink, PlantUmlLocation location)
    : ActivityInstruction(lane, inLink, location)
{
    /// <summary>
    ///     Gets the character.
    /// </summary>
    public string Text { get; } = text;

    /// <summary>
    ///     Gets the fill, or null for the theme's.
    /// </summary>
    public Rgba32? Fill { get; } = fill;

    /// <summary>
    ///     Gets a value indicating whether the flow ends after the connector (<c>detach</c>).
    /// </summary>
    public bool Killed { get; private set; }

    /// <inheritdoc/>
    public override bool Kill()
    {
        Killed = true;
        return true;
    }
}

/// <summary>
///     One branch of an if: its test, its labels and its instructions (PlantUML's <c>Branch</c>).
/// </summary>
internal sealed class ActivityBranch
{
    /// <summary>
    ///     Initializes a branch.
    /// </summary>
    /// <param name="lane">The swimlane of the if.</param>
    /// <param name="labelPositive">The label of the arrow into the branch (<c>then (yes)</c>, <c>else (no)</c>).</param>
    /// <param name="test">The test of the branch (empty for the else branch).</param>
    /// <param name="color">The fill of its diamond, or null.</param>
    /// <param name="inLabel">The label of the arrow that leads to an elseif's diamond.</param>
    public ActivityBranch(ActivityLane? lane, ActivityLink labelPositive, string test, Rgba32? color, ActivityLink inLabel)
    {
        List = new ActivityList(lane);
        LabelPositive = labelPositive;
        Test = test;
        Color = color;
        InLabel = inLabel;
    }

    /// <summary>
    ///     Gets the instructions of the branch.
    /// </summary>
    public ActivityList List { get; }

    /// <summary>
    ///     Gets the label of the arrow into the branch.
    /// </summary>
    public ActivityLink LabelPositive { get; }

    /// <summary>
    ///     Gets the test (empty for the else branch).
    /// </summary>
    public string Test { get; }

    /// <summary>
    ///     Gets the fill of the diamond, or null.
    /// </summary>
    public Rgba32? Color { get; }

    /// <summary>
    ///     Gets the label of the arrow that leads to the diamond of an elseif.
    /// </summary>
    public ActivityLink InLabel { get; }

    /// <summary>
    ///     Gets or sets the arrow that leaves the branch (the arrow written before <c>else</c> or <c>endif</c>).
    /// </summary>
    public ActivityLink InLinkRendering { get; set; } = ActivityLink.None;

    /// <summary>
    ///     Gets or sets the arrow written before the statement that ends the branch, or null.
    /// </summary>
    public ActivityLink? Special { get; set; }

    /// <summary>
    ///     Gets a value indicating whether the branch has no instruction.
    /// </summary>
    public bool IsEmpty => List.Items.Count == 0;
}

/// <summary>
///     An if with its branches: <c>if</c>, <c>elseif</c>, <c>else</c>, <c>endif</c> (PlantUML's <c>InstructionIf</c>).
/// </summary>
internal sealed class ActivityIf : ActivityInstruction
{
    /// <summary>The then branches (the if and every elseif).</summary>
    private readonly List<ActivityBranch> _thens = [];

    /// <summary>
    ///     Initializes an if.
    /// </summary>
    /// <param name="lane">The swimlane.</param>
    /// <param name="test">The test.</param>
    /// <param name="whenThen">The label of the then arrow.</param>
    /// <param name="inLink">The arrow that leads into it.</param>
    /// <param name="color">The fill of the diamond, or null.</param>
    /// <param name="location">The location.</param>
    public ActivityIf(ActivityLane? lane, string test, ActivityLink whenThen, ActivityLink inLink, Rgba32? color, PlantUmlLocation location)
        : base(lane, inLink, location)
    {
        Current = new ActivityBranch(lane, whenThen, test, color, ActivityLink.None);
        _thens.Add(Current);
    }

    /// <summary>
    ///     Gets the then branches.
    /// </summary>
    public IReadOnlyList<ActivityBranch> Thens => _thens;

    /// <summary>
    ///     Gets the else branch, or null before <c>else</c> or <c>endif</c>.
    /// </summary>
    public ActivityBranch? Else { get; private set; }

    /// <summary>
    ///     Gets the branch that receives the instructions being read.
    /// </summary>
    public ActivityBranch Current { get; private set; }

    /// <summary>
    ///     Gets a value indicating whether <c>endif</c> was read.
    /// </summary>
    public bool EndifCalled { get; private set; }

    /// <summary>
    ///     Gets or sets the arrow that leaves the if (a color written right after <c>endif</c>).
    /// </summary>
    public ActivityLink OutLink { get; set; } = ActivityLink.None;

    /// <inheritdoc/>
    public override IEnumerable<ActivityLane> Lanes =>
        base.Lanes.Concat(_thens.SelectMany(b => b.List.Lanes)).Concat(Else?.List.Lanes ?? []).Distinct();

    /// <summary>
    ///     Starts an elseif branch.
    /// </summary>
    /// <param name="inLabel">The label of the arrow to its diamond.</param>
    /// <param name="test">The test.</param>
    /// <param name="whenThen">The label of its then arrow.</param>
    /// <param name="next">The pending arrow, which leaves the branch before.</param>
    /// <param name="color">The fill of its diamond.</param>
    /// <returns>False when the if already has its else branch.</returns>
    public bool ElseIf(ActivityLink inLabel, string test, ActivityLink whenThen, ActivityLink next, Rgba32? color)
    {
        if (Else is not null)
        {
            return false;
        }

        Current.Special = next;
        Current = new ActivityBranch(Lane, whenThen, test, color, inLabel);
        _thens.Add(Current);
        return true;
    }

    /// <summary>
    ///     Starts the else branch.
    /// </summary>
    /// <param name="whenElse">The label of the else arrow.</param>
    /// <param name="next">The pending arrow, which leaves the branch before.</param>
    /// <returns>False when the if already has its else branch.</returns>
    public bool SwitchToElse(ActivityLink whenElse, ActivityLink next)
    {
        Current.Special = next;
        if (Else is not null)
        {
            return false;
        }

        Current.InLinkRendering = next;
        Else = new ActivityBranch(Lane, whenElse, string.Empty, null, ActivityLink.None);
        Current = Else;
        return true;
    }

    /// <summary>
    ///     Ends the if.
    /// </summary>
    /// <param name="next">The pending arrow, which leaves the last branch.</param>
    public void Endif(ActivityLink next)
    {
        EndifCalled = true;
        Else ??= new ActivityBranch(Lane, ActivityLink.None, string.Empty, null, ActivityLink.None);
        Else.Special = next;
        Current.InLinkRendering = next;
    }

    /// <inheritdoc/>
    public override bool AddNote(ActivityNote note) =>
        EndifCalled || Current.IsEmpty ? base.AddNote(note) : Current.List.AddNote(note);

    /// <inheritdoc/>
    public override bool Kill()
    {
        if (EndifCalled)
        {
            // PlantUML kills the last instruction of the first then branch and of the else branch
            var first = _thens[0];
            if (first.List.Last is { } last && !last.Kill())
            {
                return false;
            }

            return Else?.List.Last is not { } elseLast || elseLast.Kill();
        }

        return Current.List.Kill();
    }
}

/// <summary>
///     A while loop: <c>while (test) is (yes)</c> ... <c>endwhile (out)</c> (PlantUML's <c>InstructionWhile</c>).
/// </summary>
/// <param name="lane">The swimlane.</param>
/// <param name="test">The test.</param>
/// <param name="yes">The label of the arrow into the body.</param>
/// <param name="inLink">The arrow that leads into it.</param>
/// <param name="color">The fill of the diamond, or null.</param>
/// <param name="location">The location.</param>
internal sealed class ActivityWhile(ActivityLane? lane, string test, string? yes, ActivityLink inLink, Rgba32? color, PlantUmlLocation location)
    : ActivityInstruction(lane, inLink, location)
{
    /// <summary>
    ///     Gets the body.
    /// </summary>
    public ActivityList Body { get; } = new(lane);

    /// <summary>
    ///     Gets the test.
    /// </summary>
    public string Test { get; } = test;

    /// <summary>
    ///     Gets or sets the label of the arrow into the body, or null.
    /// </summary>
    public string? Yes { get; set; } = yes;

    /// <summary>
    ///     Gets the fill of the diamond, or null.
    /// </summary>
    public Rgba32? Color { get; } = color;

    /// <summary>
    ///     Gets or sets the arrow that leaves the loop (<c>endwhile (out)</c> and a later arrow color).
    /// </summary>
    public ActivityLink OutLink { get; set; } = ActivityLink.None;

    /// <summary>
    ///     Gets the arrow back to the diamond (the arrow written before <c>endwhile</c>).
    /// </summary>
    public ActivityLink Incoming1 { get; private set; } = ActivityLink.None;

    /// <summary>
    ///     Gets the arrow from the backward action to the diamond.
    /// </summary>
    public ActivityLink Incoming2 { get; private set; } = ActivityLink.None;

    /// <summary>
    ///     Gets the text of the backward action, or null.
    /// </summary>
    public string? Backward { get; private set; }

    /// <summary>
    ///     Gets or sets the stop or end drawn on the exit arrow (a <c>stop</c> right after <c>endwhile</c>), or null.
    /// </summary>
    public ActivityInstruction? SpecialOut { get; set; }

    /// <summary>
    ///     Gets a value indicating whether <c>endwhile</c> was read.
    /// </summary>
    public bool TestCalled { get; private set; }

    /// <summary>
    ///     Gets a value indicating whether the flow ends after the loop.
    /// </summary>
    public bool Killed { get; private set; }

    /// <summary>
    ///     Gets a value indicating whether <c>backward</c> was read.
    /// </summary>
    public bool BackwardCalled { get; private set; }

    /// <inheritdoc/>
    public override IEnumerable<ActivityLane> Lanes => Body.Lanes.Concat(base.Lanes).Distinct();

    /// <summary>
    ///     Records the arrow written before <c>endwhile</c>.
    /// </summary>
    /// <param name="incoming">The arrow.</param>
    public void SetIncoming(ActivityLink incoming)
    {
        if (!BackwardCalled)
        {
            Incoming1 = incoming;
            Incoming2 = incoming;
        }

        TestCalled = true;
    }

    /// <summary>
    ///     Records the backward action.
    /// </summary>
    /// <param name="text">Its text.</param>
    /// <param name="incoming1">The arrow into it.</param>
    /// <param name="incoming2">The arrow out of it.</param>
    public void SetBackward(string text, ActivityLink incoming1, ActivityLink incoming2)
    {
        Backward = text;
        Incoming1 = incoming1;
        Incoming2 = incoming2;
        BackwardCalled = true;
    }

    /// <inheritdoc/>
    public override bool AddNote(ActivityNote note) => Body.Items.Count == 0 ? base.AddNote(note) : Body.AddNote(note);

    /// <inheritdoc/>
    public override bool Kill()
    {
        if (TestCalled)
        {
            Killed = true;
            return true;
        }

        return Body.Kill();
    }
}

/// <summary>
///     A repeat loop: <c>repeat</c> ... <c>repeat while (test) is (yes) not (no)</c> (PlantUML's
///     <c>InstructionRepeat</c>).
/// </summary>
/// <param name="lane">The swimlane.</param>
/// <param name="startLabel">The text of the action drawn at the top of the loop, or null for a diamond.</param>
/// <param name="inLink">The arrow that leads into it.</param>
/// <param name="color">The fill of the start action or diamond, or null.</param>
/// <param name="location">The location.</param>
internal sealed class ActivityRepeat(ActivityLane? lane, string? startLabel, ActivityLink inLink, Rgba32? color, PlantUmlLocation location)
    : ActivityInstruction(lane, inLink, location)
{
    /// <summary>
    ///     Gets the body.
    /// </summary>
    public ActivityList Body { get; } = new(lane);

    /// <summary>
    ///     Gets the text of the start action, or null.
    /// </summary>
    public string? StartLabel { get; } = startLabel;

    /// <summary>
    ///     Gets the fill of the start action or diamond, or null.
    /// </summary>
    public Rgba32? Color { get; } = color;

    /// <summary>
    ///     Gets the test, or null when the loop has none.
    /// </summary>
    public string? Test { get; private set; }

    /// <summary>
    ///     Gets the label of the arrow back to the start (<c>is (yes)</c>), or null.
    /// </summary>
    public string? Yes { get; private set; }

    /// <summary>
    ///     Gets the label of the exit arrow (<c>not (no)</c>), or null.
    /// </summary>
    public string? Out { get; private set; }

    /// <summary>
    ///     Gets the arrow written before <c>repeat while</c> (from the body to the diamond).
    /// </summary>
    public ActivityLink EndRepeatLink { get; private set; } = ActivityLink.None;

    /// <summary>
    ///     Gets or sets the arrow back to the start.
    /// </summary>
    public ActivityLink Incoming1 { get; set; } = ActivityLink.None;

    /// <summary>
    ///     Gets the arrow from the backward action to the start.
    /// </summary>
    public ActivityLink Incoming2 { get; private set; } = ActivityLink.None;

    /// <summary>
    ///     Gets the text of the backward action, or null.
    /// </summary>
    public string? Backward { get; private set; }

    /// <summary>
    ///     Gets the notes of the backward action.
    /// </summary>
    public List<ActivityNote> BackwardNotes { get; } = [];

    /// <summary>
    ///     Gets a value indicating whether <c>repeat while</c> was read.
    /// </summary>
    public bool TestCalled { get; private set; }

    /// <summary>
    ///     Gets a value indicating whether the flow ends after the loop.
    /// </summary>
    public bool Killed { get; private set; }

    /// <summary>
    ///     Gets or sets the swimlane of <c>repeat while</c>, where the loop is left.
    /// </summary>
    public ActivityLane? LaneOut { get; set; } = lane;

    /// <summary>
    ///     Gets or sets the swimlane of the backward action.
    /// </summary>
    public ActivityLane? BackwardLane { get; set; } = lane;

    /// <inheritdoc/>
    public override IEnumerable<ActivityLane> Lanes => Body.Lanes.Concat(base.Lanes).Distinct();

    /// <summary>
    ///     Ends the loop with its test.
    /// </summary>
    /// <param name="test">The test, or null.</param>
    /// <param name="yes">The label of the arrow back.</param>
    /// <param name="exit">The label of the exit arrow.</param>
    /// <param name="endRepeat">The arrow from the body to the diamond.</param>
    /// <param name="back">The arrow back written after the test.</param>
    public void SetTest(string? test, string? yes, string? exit, ActivityLink endRepeat, ActivityLink back)
    {
        Test = test;
        Yes = yes;
        Out = exit;
        EndRepeatLink = endRepeat;
        if (!back.IsNone)
        {
            Incoming1 = back;
        }

        TestCalled = true;
    }

    /// <summary>
    ///     Records the backward action.
    /// </summary>
    /// <param name="text">Its text.</param>
    /// <param name="incoming1">The arrow into it.</param>
    /// <param name="incoming2">The arrow out of it.</param>
    public void SetBackward(string text, ActivityLink incoming1, ActivityLink incoming2)
    {
        Backward = text;
        Incoming1 = incoming1;
        Incoming2 = incoming2;
    }

    /// <inheritdoc/>
    public override bool AddNote(ActivityNote note)
    {
        if (Backward is null)
        {
            return Body.AddNote(note);
        }

        BackwardNotes.Add(note);
        return true;
    }

    /// <inheritdoc/>
    public override bool Kill()
    {
        if (TestCalled)
        {
            Killed = true;
            return true;
        }

        return Body.Kill();
    }
}

/// <summary>
///     How a fork ends.
/// </summary>
internal enum ActivityForkStyle
{
    /// <summary>With a bar (<c>end fork</c>).</summary>
    Fork,

    /// <summary>With a diamond (<c>end merge</c>).</summary>
    Merge,
}

/// <summary>
///     A fork: <c>fork</c> ... <c>fork again</c> ... <c>end fork</c> or <c>end merge</c> (PlantUML's
///     <c>InstructionFork</c>).
/// </summary>
internal sealed class ActivityFork : ActivityInstruction
{
    /// <summary>The branches.</summary>
    private readonly List<ActivityList> _branches = [];

    /// <summary>
    ///     Initializes a fork.
    /// </summary>
    /// <param name="lane">The swimlane.</param>
    /// <param name="inLink">The arrow that leads into it.</param>
    /// <param name="location">The location.</param>
    public ActivityFork(ActivityLane? lane, ActivityLink inLink, PlantUmlLocation location)
        : base(lane, inLink, location)
    {
        LaneOut = lane;
        _branches.Add(new ActivityList(null));
    }

    /// <summary>
    ///     Gets the branches.
    /// </summary>
    public IReadOnlyList<ActivityList> Branches => _branches;

    /// <summary>
    ///     Gets or sets how the fork ends.
    /// </summary>
    public ActivityForkStyle Style { get; set; } = ActivityForkStyle.Fork;

    /// <summary>
    ///     Gets or sets the label written after <c>end fork</c>, or null.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    ///     Gets or sets the swimlane of the bar that ends the fork.
    /// </summary>
    public ActivityLane? LaneOut { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the fork is ended.
    /// </summary>
    public bool Finished { get; set; }

    /// <summary>
    ///     Gets the branch being read.
    /// </summary>
    public ActivityList Last => _branches[^1];

    /// <inheritdoc/>
    public override IEnumerable<ActivityLane> Lanes =>
        _branches.SelectMany(b => b.Lanes).Concat(base.Lanes).Concat(LaneOut is null ? [] : [LaneOut]).Distinct();

    /// <summary>
    ///     Starts a new branch.
    /// </summary>
    /// <param name="lane">The current swimlane.</param>
    public void ForkAgain(ActivityLane? lane)
    {
        LaneOut = lane;
        _branches.Add(new ActivityList(null));
    }

    /// <inheritdoc/>
    public override bool AddNote(ActivityNote note) => Finished ? base.AddNote(note) : Last.AddNote(note);

    /// <inheritdoc/>
    public override bool Kill() => Last.Kill();
}

/// <summary>
///     A split: <c>split</c> ... <c>split again</c> ... <c>end split</c> (PlantUML's <c>InstructionSplit</c>).
/// </summary>
internal sealed class ActivitySplit : ActivityInstruction
{
    /// <summary>The branches.</summary>
    private readonly List<ActivityList> _branches = [];

    /// <summary>
    ///     Initializes a split.
    /// </summary>
    /// <param name="lane">The swimlane.</param>
    /// <param name="inLink">The arrow that leads into it.</param>
    /// <param name="location">The location.</param>
    public ActivitySplit(ActivityLane? lane, ActivityLink inLink, PlantUmlLocation location)
        : base(lane, inLink, location)
    {
        LaneOut = lane;
        _branches.Add(new ActivityList(lane));
    }

    /// <summary>
    ///     Gets the branches.
    /// </summary>
    public IReadOnlyList<ActivityList> Branches => _branches;

    /// <summary>
    ///     Gets or sets the swimlane the split ends in.
    /// </summary>
    public ActivityLane? LaneOut { get; set; }

    /// <summary>
    ///     Gets the branch being read.
    /// </summary>
    public ActivityList Last => _branches[^1];

    /// <inheritdoc/>
    public override IEnumerable<ActivityLane> Lanes => _branches.SelectMany(b => b.Lanes).Distinct();

    /// <summary>
    ///     Starts a new branch.
    /// </summary>
    public void SplitAgain() => _branches.Add(new ActivityList(Lane));

    /// <inheritdoc/>
    public override bool AddNote(ActivityNote note) => Last.AddNote(note);

    /// <inheritdoc/>
    public override bool Kill() => Last.Kill();
}

/// <summary>
///     A group of instructions in a frame: <c>partition Name {</c> ... <c>}</c> (PlantUML's <c>InstructionGroup</c>).
/// </summary>
/// <param name="lane">The swimlane.</param>
/// <param name="title">The title.</param>
/// <param name="fill">The fill, or null.</param>
/// <param name="framed">Whether the frame is drawn (false for a group shape this version does not draw).</param>
/// <param name="inLink">The arrow that leads into it.</param>
/// <param name="location">The location.</param>
internal sealed class ActivityGroup(ActivityLane? lane, string title, Rgba32? fill, bool framed, ActivityLink inLink, PlantUmlLocation location)
    : ActivityInstruction(lane, inLink, location)
{
    /// <summary>
    ///     Gets the instructions of the group.
    /// </summary>
    public ActivityList Body { get; } = new(lane);

    /// <summary>
    ///     Gets the title.
    /// </summary>
    public string Title { get; } = title;

    /// <summary>
    ///     Gets the fill, or null.
    /// </summary>
    public Rgba32? Fill { get; } = fill;

    /// <summary>
    ///     Gets a value indicating whether the frame is drawn (a <c>partition</c> or <c>group</c>); the instructions of
    ///     a <c>package</c>, <c>rectangle</c> or <c>card</c> are drawn without it.
    /// </summary>
    public bool Framed { get; } = framed;

    /// <inheritdoc/>
    public override IEnumerable<ActivityLane> Lanes => Body.Lanes;

    /// <inheritdoc/>
    public override bool AddNote(ActivityNote note) => Body.Items.Count == 0 ? base.AddNote(note) : Body.AddNote(note);

    /// <inheritdoc/>
    public override bool Kill() => Body.Kill();
}
