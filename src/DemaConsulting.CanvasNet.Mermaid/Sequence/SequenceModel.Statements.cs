namespace DemaConsulting.CanvasNet.Mermaid.Sequence;

/// <summary>
///     One statement for the sequence database: the objects that Mermaid's grammar builds and hands, all at once after
///     the whole text has parsed, to the database's <c>apply</c>.
/// </summary>
/// <remarks>
///     The parser lists them in Mermaid's order (the order of the flattened statement arrays of the grammar), so
///     applying them in turn makes the same database calls as Mermaid. Statements are immutable.
/// </remarks>
/// <param name="Location">The location of the statement in the original text, for the errors of the database.</param>
internal abstract record SequenceStatement(MermaidLocation Location);

/// <summary>
///     The text of an <c>@{ ... }</c> block of a participant, trimmed, with the map of its positions to the original
///     text.
/// </summary>
/// <param name="Text">The text between the braces, trimmed.</param>
/// <param name="Locate">Maps a position of <see cref="Text"/> to its location in the original text.</param>
internal sealed record SequenceMetadataText(string Text, Func<int, MermaidLocation> Locate);

/// <summary>
///     A participant declaration or mention (Mermaid's <c>addParticipant</c> and <c>createParticipant</c>).
/// </summary>
/// <param name="Actor">The participant id.</param>
/// <param name="Draw">The keyword type (<c>participant</c> or <c>actor</c>), or null for a mention in another statement.</param>
/// <param name="Description">The alias after <c>as</c>, or null.</param>
/// <param name="Config">The <c>@{ ... }</c> metadata, or null.</param>
/// <param name="Create">Whether the statement is a <c>create</c>.</param>
/// <param name="Location">The location of the statement.</param>
internal sealed record SequenceParticipantStatement(
    string Actor,
    string? Draw,
    SequenceText? Description,
    SequenceMetadataText? Config,
    bool Create,
    MermaidLocation Location) : SequenceStatement(Location);

/// <summary>
///     A <c>destroy</c> statement (Mermaid's <c>destroyParticipant</c>).
/// </summary>
/// <param name="Actor">The participant id.</param>
/// <param name="Location">The location of the statement.</param>
internal sealed record SequenceDestroyStatement(string Actor, MermaidLocation Location) : SequenceStatement(Location);

/// <summary>
///     A signal about one participant: an activation, a deactivation or a central connection (Mermaid's
///     <c>activeStart</c>, <c>activeEnd</c>, <c>centralConnection</c> and <c>centralConnectionReverse</c>).
/// </summary>
/// <param name="Actor">The participant id.</param>
/// <param name="Type">The line type.</param>
/// <param name="Location">The location of the statement.</param>
internal sealed record SequenceActorSignalStatement(string Actor, int Type, MermaidLocation Location) : SequenceStatement(Location);

/// <summary>
///     A message between two participants (Mermaid's <c>addMessage</c>).
/// </summary>
/// <param name="From">The source participant id.</param>
/// <param name="To">The target participant id.</param>
/// <param name="Type">The line type of the arrow.</param>
/// <param name="Text">The message text.</param>
/// <param name="Activate">Whether the message activates its target.</param>
/// <param name="CentralConnection">The central connection line type, or 0.</param>
/// <param name="Location">The location of the statement.</param>
internal sealed record SequenceMessageStatement(
    string From,
    string To,
    int Type,
    SequenceText Text,
    bool Activate,
    int CentralConnection,
    MermaidLocation Location) : SequenceStatement(Location);

/// <summary>
///     A note (Mermaid's <c>addNote</c>).
/// </summary>
/// <param name="From">The first participant id.</param>
/// <param name="To">The second participant id (the first again for a note on one participant).</param>
/// <param name="Placement">The placement (<see cref="SequencePlacement"/>).</param>
/// <param name="Text">The note text.</param>
/// <param name="Location">The location of the statement.</param>
internal sealed record SequenceNoteStatement(string From, string To, int Placement, SequenceText Text, MermaidLocation Location) : SequenceStatement(Location);

/// <summary>
///     The interaction statements that attach data to a participant.
/// </summary>
internal enum SequenceActorDataKind
{
    /// <summary><c>links</c>: a JSON object of labels and URLs.</summary>
    Links,

    /// <summary><c>link</c>: one <c>label @ url</c>.</summary>
    Link,

    /// <summary><c>properties</c>: a JSON object.</summary>
    Properties,

    /// <summary><c>details</c>: the id of a page element, which has no effect outside a browser page.</summary>
    Details,
}

/// <summary>
///     A <c>links</c>, <c>link</c>, <c>properties</c> or <c>details</c> statement.
/// </summary>
/// <param name="Kind">The statement.</param>
/// <param name="Actor">The participant id.</param>
/// <param name="Text">The text after the colon.</param>
/// <param name="Location">The location of the statement.</param>
internal sealed record SequenceActorDataStatement(SequenceActorDataKind Kind, string Actor, SequenceText Text, MermaidLocation Location) : SequenceStatement(Location);

/// <summary>
///     The start of a <c>box</c> (Mermaid's <c>boxStart</c>).
/// </summary>
/// <param name="Data">The color and title.</param>
/// <param name="Location">The location of the statement.</param>
internal sealed record SequenceBoxStartStatement(SequenceBoxData Data, MermaidLocation Location) : SequenceStatement(Location);

/// <summary>
///     The end of a <c>box</c> (Mermaid's <c>boxEnd</c>).
/// </summary>
/// <param name="Location">The location of the <c>end</c>.</param>
internal sealed record SequenceBoxEndStatement(MermaidLocation Location) : SequenceStatement(Location);

/// <summary>
///     A marker of a block: its start, a section or its end (the <c>loopStart</c>, <c>else</c>, <c>altEnd</c> and the
///     like of Mermaid's grammar).
/// </summary>
/// <param name="Type">The line type of the marker.</param>
/// <param name="Text">The text (a title, or the color of a <c>rect</c>), or null for an end.</param>
/// <param name="Location">The location of the keyword.</param>
internal sealed record SequenceBlockStatement(int Type, SequenceText? Text, MermaidLocation Location) : SequenceStatement(Location);

/// <summary>
///     An <c>autonumber</c> statement (Mermaid's <c>sequenceIndex</c>).
/// </summary>
/// <param name="Start">The first number, or null.</param>
/// <param name="Step">The step, or null.</param>
/// <param name="Visible">False for <c>autonumber off</c>.</param>
/// <param name="Location">The location of the statement.</param>
internal sealed record SequenceAutonumberStatement(double? Start, double? Step, bool Visible, MermaidLocation Location) : SequenceStatement(Location);
