// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Svek;

namespace DemaConsulting.CanvasNet.PlantUml.State;

/// <summary>
///     The kind of an element of a state diagram (PlantUML's leaf types of the state diagram).
/// </summary>
internal enum StateKind
{
    /// <summary>A state: a rounded box with its name and its descriptions, or a composite state.</summary>
    State,

    /// <summary>The start pseudo-state (<c>[*]</c> as a source, <c>&lt;&lt;start&gt;&gt;</c>): a filled circle.</summary>
    Start,

    /// <summary>The end pseudo-state (<c>[*]</c> as a target, <c>&lt;&lt;end&gt;&gt;</c>): a ring around a filled circle.</summary>
    End,

    /// <summary>A choice (<c>&lt;&lt;choice&gt;&gt;</c>): a small diamond.</summary>
    Choice,

    /// <summary>A fork or a join (<c>&lt;&lt;fork&gt;&gt;</c>, <c>&lt;&lt;join&gt;&gt;</c>): a black bar.</summary>
    ForkJoin,

    /// <summary>A junction (<c>&lt;&lt;junction&gt;&gt;</c>): a small filled circle.</summary>
    Junction,

    /// <summary>The shallow history (<c>[H]</c>, <c>X[H]</c>, <c>&lt;&lt;history&gt;&gt;</c>): a circle with an H.</summary>
    History,

    /// <summary>The deep history (<c>[H*]</c>, <c>X[H*]</c>, <c>&lt;&lt;history*&gt;&gt;</c>): a circle with H*.</summary>
    DeepHistory,
}

/// <summary>
///     A node of a state diagram: an element (a state or a pseudo-state) or a note.
/// </summary>
/// <param name="id">The id: the qualified name PlantUML gives the node (<c>Active.Online</c>, <c>.start.</c>).</param>
/// <param name="location">The location of the statement that created it.</param>
internal abstract class StateNode(string id, PlantUmlLocation location)
{
    /// <summary>
    ///     Gets the id.
    /// </summary>
    public string Id { get; } = id;

    /// <summary>
    ///     Gets the location of the statement that created the node.
    /// </summary>
    public PlantUmlLocation Location { get; } = location;

    /// <summary>
    ///     Gets or sets the region of a composite state the node is in, or null at the top of the diagram.
    /// </summary>
    public StateRegion? Region { get; set; }
}

/// <summary>
///     A state or a pseudo-state of a state diagram; a state with regions is a composite state.
/// </summary>
/// <param name="id">The id.</param>
/// <param name="display">The name that is drawn.</param>
/// <param name="kind">The kind.</param>
/// <param name="location">The location of the statement that created it.</param>
internal sealed class StateElement(string id, string display, StateKind kind, PlantUmlLocation location) : StateNode(id, location)
{
    /// <summary>
    ///     Gets or sets the name that is drawn.
    /// </summary>
    public string Display { get; set; } = display;

    /// <summary>
    ///     Gets or sets the kind.
    /// </summary>
    public StateKind Kind { get; set; } = kind;

    /// <summary>
    ///     Gets the description lines (<c>state X : text</c>, <c>X : text</c>), in order.
    /// </summary>
    public List<string> Descriptions { get; } = [];

    /// <summary>
    ///     Gets or sets the fill (<c>#color</c>), or null for the theme's.
    /// </summary>
    public Rgba32? Fill { get; set; }

    /// <summary>
    ///     Gets or sets the line color (<c>##color</c>), or null for the theme's.
    /// </summary>
    public Rgba32? Line { get; set; }

    /// <summary>
    ///     Gets the regions of a composite state, in order (empty for a simple state); the first region holds what is
    ///     written before the first <c>--</c> or <c>||</c>.
    /// </summary>
    public List<StateRegion> Regions { get; } = [];

    /// <summary>
    ///     Gets or sets how the regions are arranged: stacked (<c>--</c>) or side by side (<c>||</c>); none for one
    ///     region.
    /// </summary>
    public ClusterConcurrency Concurrency { get; set; }

    /// <summary>
    ///     Gets a value indicating whether the state is a composite state.
    /// </summary>
    public bool IsComposite => Regions.Count > 0;

    /// <summary>
    ///     Gets the nesting depth: zero at the top of the diagram, one inside a composite state, and so on.
    /// </summary>
    public int Depth
    {
        get
        {
            var depth = 0;
            for (var region = Region; region is not null; region = region.Owner.Region)
            {
                depth++;
            }

            return depth;
        }
    }
}

/// <summary>
///     A region of a composite state: the part before the first concurrent separator, or after one.
/// </summary>
/// <param name="prefix">The prefix of the ids of its members (the composite's id, or <c>Composite.CONCn</c>).</param>
/// <param name="owner">The composite state.</param>
/// <param name="index">The index of the region in the composite (0 for the first).</param>
internal sealed class StateRegion(string prefix, StateElement owner, int index)
{
    /// <summary>
    ///     Gets the prefix of the ids of the members (PlantUML's qualified name of the region).
    /// </summary>
    public string Prefix { get; } = prefix;

    /// <summary>
    ///     Gets the composite state.
    /// </summary>
    public StateElement Owner { get; } = owner;

    /// <summary>
    ///     Gets the index of the region in its composite.
    /// </summary>
    public int Index { get; } = index;

    /// <summary>
    ///     Gets the last segment of the prefix (the name PlantUML gives the pseudo-states of the region:
    ///     <c>Active..start.Active</c>).
    /// </summary>
    public string ShortName => Prefix[(Prefix.LastIndexOf('.') + 1)..];

    /// <summary>
    ///     Gets a value indicating whether the region is one of the concurrent regions after the first (PlantUML's
    ///     <c>CONCURRENT_STATE</c> groups, which no transition may leave).
    /// </summary>
    public bool IsConcurrent => Index > 0;
}

/// <summary>
///     A note of a state diagram: attached to a side of a state, to the last transition, or floating.
/// </summary>
/// <param name="id">The id (its alias, or a generated id).</param>
/// <param name="text">The text.</param>
/// <param name="location">The location.</param>
internal sealed class StateNote(string id, string text, PlantUmlLocation location) : StateNode(id, location)
{
    /// <summary>
    ///     Gets the text.
    /// </summary>
    public string Text { get; } = text;

    /// <summary>
    ///     Gets or sets the fill, or null for the theme's.
    /// </summary>
    public Rgba32? Fill { get; set; }

    /// <summary>
    ///     Gets or sets the element the note is attached to, or null.
    /// </summary>
    public StateElement? Target { get; set; }

    /// <summary>
    ///     Gets or sets the transition the note is attached to, or null.
    /// </summary>
    public StateTransition? OnLink { get; set; }

    /// <summary>
    ///     Gets or sets the side of the target the note is on.
    /// </summary>
    public NotePosition Position { get; set; } = NotePosition.Right;

    /// <summary>
    ///     Gets a value indicating whether the note floats (has no target and no transition).
    /// </summary>
    public bool IsFloating => Target is null && OnLink is null;
}

/// <summary>
///     A transition of a state diagram (or a link to a floating note), with its arrow's settings and its label.
/// </summary>
/// <param name="id">The id.</param>
/// <param name="from">The first end as written.</param>
/// <param name="to">The second end as written.</param>
/// <param name="location">The location of the statement.</param>
internal sealed class StateTransition(string id, StateNode from, StateNode to, PlantUmlLocation location)
{
    /// <summary>Gets the id.</summary>
    public string Id { get; } = id;

    /// <summary>Gets the first end.</summary>
    public StateNode From { get; } = from;

    /// <summary>Gets the second end.</summary>
    public StateNode To { get; } = to;

    /// <summary>Gets the location of the statement.</summary>
    public PlantUmlLocation Location { get; } = location;

    /// <summary>Gets the decoration at the first end (an arrow head for <c>&lt;--</c>).</summary>
    public LinkDecor TailDecor { get; init; }

    /// <summary>Gets the decoration at the second end (an arrow head for <c>--&gt;</c>).</summary>
    public LinkDecor HeadDecor { get; init; }

    /// <summary>Gets the line style.</summary>
    public LinkStyle Style { get; init; }

    /// <summary>Gets the line width, or null.</summary>
    public double? Thickness { get; init; }

    /// <summary>Gets the line color, or null.</summary>
    public Rgba32? Color { get; init; }

    /// <summary>Gets the length of the arrow body.</summary>
    public int Length { get; init; } = 2;

    /// <summary>Gets the direction hint.</summary>
    public LinkDirection Direction { get; init; }

    /// <summary>Gets the label (<c>: event [guard] / action</c>), or null.</summary>
    public string? Label { get; init; }

    /// <summary>Gets a value indicating whether the transition touches a note (it is not counted).</summary>
    public bool TouchesNote => From is StateNote || To is StateNote;
}
