using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.PlantUml.Sequence;

/// <summary>
///     The shape of a participant.
/// </summary>
internal enum ParticipantKind
{
    /// <summary>A box (<c>participant</c>).</summary>
    Participant,

    /// <summary>A stick figure (<c>actor</c>).</summary>
    Actor,

    /// <summary>A circle on a T-bar (<c>boundary</c>).</summary>
    Boundary,

    /// <summary>A circle with an arrow (<c>control</c>).</summary>
    Control,

    /// <summary>An underlined circle (<c>entity</c>).</summary>
    Entity,

    /// <summary>A cylinder (<c>database</c>).</summary>
    Database,

    /// <summary>Two stacked boxes (<c>collections</c>).</summary>
    Collections,

    /// <summary>A horizontal cylinder (<c>queue</c>).</summary>
    Queue,
}

/// <summary>
///     A participant of a sequence diagram.
/// </summary>
/// <param name="Code">The name messages use (the alias when one is given).</param>
/// <param name="Display">The text drawn in the head and the tail.</param>
/// <param name="Kind">The shape.</param>
/// <param name="Location">The location of the declaration or first use.</param>
internal sealed record SequenceParticipant(string Code, string Display, ParticipantKind Kind, PlantUmlLocation Location)
{
    /// <summary>
    ///     Gets the fill written after the declaration (<c>#color</c>), or null for the theme's.
    /// </summary>
    public Rgba32? Fill { get; init; }

    /// <summary>
    ///     Gets the <c>order</c> value of the declaration, or null.
    /// </summary>
    public int? Order { get; init; }
}

/// <summary>
///     A box around consecutive participants (<c>box "title" #color</c> ... <c>end box</c>).
/// </summary>
/// <param name="Title">The title, or null.</param>
/// <param name="First">The index of the first participant in the box.</param>
/// <param name="Last">The index of the last participant in the box.</param>
/// <param name="Location">The location of the <c>box</c> statement.</param>
internal sealed record SequenceBox(string? Title, int First, int Last, PlantUmlLocation Location)
{
    /// <summary>
    ///     Gets the fill, or null for the theme's.
    /// </summary>
    public Rgba32? Fill { get; init; }
}

/// <summary>
///     The decoration at one end of an arrow.
/// </summary>
internal enum ArrowHead
{
    /// <summary>No head.</summary>
    None,

    /// <summary>A filled head (<c>&gt;</c>).</summary>
    Filled,

    /// <summary>An open head of two lines (<c>&gt;&gt;</c>).</summary>
    Open,

    /// <summary>The upper half of a filled head (<c>\</c>).</summary>
    HalfTop,

    /// <summary>The lower half of a filled head (<c>/</c>).</summary>
    HalfBottom,

    /// <summary>A cross: the message is lost (<c>x</c>).</summary>
    Cross,
}

/// <summary>
///     The look of a message arrow: its body and the decorations of both ends.
/// </summary>
/// <param name="Dotted">Whether the body is dotted (<c>--&gt;</c>).</param>
/// <param name="FromHead">The decoration at the end of the sender.</param>
/// <param name="ToHead">The decoration at the end of the receiver.</param>
internal sealed record ArrowStyle(bool Dotted, ArrowHead FromHead, ArrowHead ToHead)
{
    /// <summary>
    ///     Gets a value indicating whether a circle is drawn at the end of the sender (<c>o-&gt;</c>).
    /// </summary>
    public bool FromCircle { get; init; }

    /// <summary>
    ///     Gets a value indicating whether a circle is drawn at the end of the receiver (<c>-&gt;o</c>).
    /// </summary>
    public bool ToCircle { get; init; }

    /// <summary>
    ///     Gets the color of the arrow (<c>-[#red]&gt;</c>), or null for the theme's.
    /// </summary>
    public Rgba32? Color { get; init; }
}

/// <summary>
///     A change of the life of a participant.
/// </summary>
internal enum LifeAction
{
    /// <summary>An activation starts (<c>activate</c>, <c>++</c>).</summary>
    Activate,

    /// <summary>The latest activation ends (<c>deactivate</c>, <c>--</c>).</summary>
    Deactivate,

    /// <summary>The life line ends with a cross (<c>destroy</c>, <c>!!</c>).</summary>
    Destroy,
}

/// <summary>
///     A change of the life of one participant.
/// </summary>
/// <param name="Participant">The index of the participant.</param>
/// <param name="Action">The change.</param>
/// <param name="Fill">The fill of an activation, or null for the theme's.</param>
internal sealed record LifeChange(int Participant, LifeAction Action, Rgba32? Fill);

/// <summary>
///     One event of a sequence diagram, in the order the diagram draws them from top to bottom.
/// </summary>
internal abstract record SequenceEvent
{
    /// <summary>
    ///     Gets the location of the statement.
    /// </summary>
    public PlantUmlLocation Location { get; init; }
}

/// <summary>
///     A message between two participants (or a participant and itself).
/// </summary>
/// <param name="From">The index of the sender.</param>
/// <param name="To">The index of the receiver.</param>
/// <param name="Label">The label text (creole allowed), possibly empty.</param>
/// <param name="Arrow">The look of the arrow.</param>
internal sealed record SequenceMessage(int From, int To, string Label, ArrowStyle Arrow) : SequenceEvent
{
    /// <summary>
    ///     Gets the number drawn before the label (<c>autonumber</c>), or null.
    /// </summary>
    public string? Number { get; init; }

    /// <summary>
    ///     Gets the life changes that happen on the arrow (<c>++</c>, <c>--</c>, <c>!!</c>, and <c>activate</c> or
    ///     <c>deactivate</c> statements that follow the message).
    /// </summary>
    public IReadOnlyList<LifeChange> Changes { get; init; } = [];

    /// <summary>
    ///     Gets a value indicating whether the message goes from a participant to itself.
    /// </summary>
    public bool IsSelf => From == To;
}

/// <summary>
///     A life change that is not on a message arrow (<c>activate X</c> before any message to it).
/// </summary>
/// <param name="Change">The change.</param>
internal sealed record SequenceLifeEvent(LifeChange Change) : SequenceEvent;

/// <summary>
///     Where a note is drawn.
/// </summary>
internal enum NotePosition
{
    /// <summary>Left of a participant.</summary>
    Left,

    /// <summary>Right of a participant.</summary>
    Right,

    /// <summary>Over one participant, or across several.</summary>
    Over,
}

/// <summary>
///     The outline of a note.
/// </summary>
internal enum NoteShape
{
    /// <summary>A sheet with a folded corner (<c>note</c>).</summary>
    Note,

    /// <summary>A hexagon (<c>hnote</c>).</summary>
    Hexagon,

    /// <summary>A rectangle (<c>rnote</c>).</summary>
    Box,
}

/// <summary>
///     A note.
/// </summary>
/// <param name="Position">Where it is drawn.</param>
/// <param name="First">The index of the participant (the first one of a note across several).</param>
/// <param name="Last">The index of the last participant of a note across several; else <paramref name="First"/>.</param>
/// <param name="Text">The text (creole allowed).</param>
/// <param name="Shape">The outline.</param>
internal sealed record SequenceNote(NotePosition Position, int First, int Last, string Text, NoteShape Shape) : SequenceEvent
{
    /// <summary>
    ///     Gets the fill, or null for the theme's.
    /// </summary>
    public Rgba32? Fill { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the note is drawn on the row of the message before it (<c>note left</c> or
    ///     <c>note right</c> without a participant, right after a message).
    /// </summary>
    public bool OnMessage { get; init; }
}

/// <summary>
///     The start of a group (<c>alt</c>, <c>opt</c>, <c>loop</c>, <c>par</c>, <c>break</c>, <c>critical</c>,
///     <c>group</c>).
/// </summary>
/// <param name="Keyword">The keyword drawn in the header tab (for <c>group</c>, its first text).</param>
/// <param name="Label">The condition drawn in brackets after the tab, or null.</param>
internal sealed record SequenceGroupStart(string Keyword, string? Label) : SequenceEvent
{
    /// <summary>
    ///     Gets the fill of the frame, or null for none.
    /// </summary>
    public Rgba32? Fill { get; init; }
}

/// <summary>
///     A new section of the innermost group (<c>else</c>).
/// </summary>
/// <param name="Label">The condition drawn in brackets, or null.</param>
internal sealed record SequenceGroupElse(string? Label) : SequenceEvent;

/// <summary>
///     The end of the innermost group (<c>end</c>).
/// </summary>
internal sealed record SequenceGroupEnd : SequenceEvent;

/// <summary>
///     A divider across the diagram (<c>== text ==</c>).
/// </summary>
/// <param name="Text">The text, possibly empty.</param>
internal sealed record SequenceDivider(string Text) : SequenceEvent;

/// <summary>
///     A delay: dotted life lines (<c>...</c> or <c>... text ...</c>).
/// </summary>
/// <param name="Text">The text, possibly empty.</param>
internal sealed record SequenceDelay(string Text) : SequenceEvent;

/// <summary>
///     Vertical space (<c>|||</c> or <c>||N||</c>).
/// </summary>
/// <param name="Height">The height in pixels.</param>
internal sealed record SequenceSpace(double Height) : SequenceEvent;
