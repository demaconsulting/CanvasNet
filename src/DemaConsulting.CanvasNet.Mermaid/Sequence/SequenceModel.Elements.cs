// Portions derived from mermaid (MIT); see third-party/README.md
using System.Globalization;
using System.Text.Json.Nodes;

namespace DemaConsulting.CanvasNet.Mermaid.Sequence;

/// <summary>
///     The line types of Mermaid's sequence database (its <c>LINETYPE</c>): the type of every message, note, block
///     marker, activation and autonumber statement.
/// </summary>
/// <remarks>
///     The values are Mermaid's, so the renderer's port can switch on them as <c>sequenceRenderer.ts</c> does and the
///     tests can compare them with Mermaid's database. The class only holds constants and is safe for concurrent use.
/// </remarks>
internal static class SequenceLineType
{
    /// <summary>A solid line with an arrowhead (<c>-&gt;&gt;</c>).</summary>
    public const int Solid = 0;

    /// <summary>A dotted line with an arrowhead (<c>--&gt;&gt;</c>).</summary>
    public const int Dotted = 1;

    /// <summary>A note.</summary>
    public const int Note = 2;

    /// <summary>A solid line with a cross (<c>-x</c>).</summary>
    public const int SolidCross = 3;

    /// <summary>A dotted line with a cross (<c>--x</c>).</summary>
    public const int DottedCross = 4;

    /// <summary>A solid line without a head (<c>-&gt;</c>).</summary>
    public const int SolidOpen = 5;

    /// <summary>A dotted line without a head (<c>--&gt;</c>).</summary>
    public const int DottedOpen = 6;

    /// <summary>The start of a <c>loop</c> block.</summary>
    public const int LoopStart = 10;

    /// <summary>The end of a <c>loop</c> block.</summary>
    public const int LoopEnd = 11;

    /// <summary>The start of an <c>alt</c> block.</summary>
    public const int AltStart = 12;

    /// <summary>An <c>else</c> section of an <c>alt</c> block.</summary>
    public const int AltElse = 13;

    /// <summary>The end of an <c>alt</c> block.</summary>
    public const int AltEnd = 14;

    /// <summary>The start of an <c>opt</c> block.</summary>
    public const int OptStart = 15;

    /// <summary>The end of an <c>opt</c> block.</summary>
    public const int OptEnd = 16;

    /// <summary>An activation of a participant.</summary>
    public const int ActiveStart = 17;

    /// <summary>A deactivation of a participant.</summary>
    public const int ActiveEnd = 18;

    /// <summary>The start of a <c>par</c> block.</summary>
    public const int ParStart = 19;

    /// <summary>An <c>and</c> section of a <c>par</c> or <c>par_over</c> block.</summary>
    public const int ParAnd = 20;

    /// <summary>The end of a <c>par</c> or <c>par_over</c> block.</summary>
    public const int ParEnd = 21;

    /// <summary>The start of a <c>rect</c> background.</summary>
    public const int RectStart = 22;

    /// <summary>The end of a <c>rect</c> background.</summary>
    public const int RectEnd = 23;

    /// <summary>A solid line with an open arrowhead (<c>-)</c>).</summary>
    public const int SolidPoint = 24;

    /// <summary>A dotted line with an open arrowhead (<c>--)</c>).</summary>
    public const int DottedPoint = 25;

    /// <summary>An <c>autonumber</c> statement.</summary>
    public const int Autonumber = 26;

    /// <summary>The start of a <c>critical</c> block.</summary>
    public const int CriticalStart = 27;

    /// <summary>An <c>option</c> section of a <c>critical</c> block.</summary>
    public const int CriticalOption = 28;

    /// <summary>The end of a <c>critical</c> block.</summary>
    public const int CriticalEnd = 29;

    /// <summary>The start of a <c>break</c> block.</summary>
    public const int BreakStart = 30;

    /// <summary>The end of a <c>break</c> block.</summary>
    public const int BreakEnd = 31;

    /// <summary>The start of a <c>par_over</c> block.</summary>
    public const int ParOverStart = 32;

    /// <summary>A solid line with arrowheads at both ends (<c>&lt;&lt;-&gt;&gt;</c>).</summary>
    public const int BidirectionalSolid = 33;

    /// <summary>A dotted line with arrowheads at both ends (<c>&lt;&lt;--&gt;&gt;</c>).</summary>
    public const int BidirectionalDotted = 34;

    /// <summary>A solid line with a filled upper half arrowhead (<c>-|\</c>).</summary>
    public const int SolidTop = 41;

    /// <summary>A solid line with a filled lower half arrowhead (<c>-|/</c>).</summary>
    public const int SolidBottom = 42;

    /// <summary>A solid line with an upper stick arrowhead (<c>-\\</c>).</summary>
    public const int StickTop = 43;

    /// <summary>A solid line with a lower stick arrowhead (<c>-//</c>).</summary>
    public const int StickBottom = 44;

    /// <summary>A solid line with a filled half arrowhead at its start (<c>/|-</c>).</summary>
    public const int SolidArrowTopReverse = 45;

    /// <summary>A solid line with a filled half arrowhead at its start (<c>\|-</c>).</summary>
    public const int SolidArrowBottomReverse = 46;

    /// <summary>A solid line with a stick arrowhead at its start (<c>//-</c>).</summary>
    public const int StickArrowTopReverse = 47;

    /// <summary>A solid line with a stick arrowhead at its start (<c>\\-</c>).</summary>
    public const int StickArrowBottomReverse = 48;

    /// <summary>A dotted line with a filled upper half arrowhead (<c>--|\</c>).</summary>
    public const int SolidTopDotted = 51;

    /// <summary>A dotted line with a filled lower half arrowhead (<c>--|/</c>).</summary>
    public const int SolidBottomDotted = 52;

    /// <summary>A dotted line with an upper stick arrowhead (<c>--\\</c>).</summary>
    public const int StickTopDotted = 53;

    /// <summary>A dotted line with a lower stick arrowhead (<c>--//</c>).</summary>
    public const int StickBottomDotted = 54;

    /// <summary>A dotted line with a filled half arrowhead at its start (<c>/|--</c>).</summary>
    public const int SolidArrowTopReverseDotted = 55;

    /// <summary>A dotted line with a filled half arrowhead at its start (<c>\|--</c>).</summary>
    public const int SolidArrowBottomReverseDotted = 56;

    /// <summary>A dotted line with a stick arrowhead at its start (<c>//--</c>).</summary>
    public const int StickArrowTopReverseDotted = 57;

    /// <summary>A dotted line with a stick arrowhead at its start (<c>\\--</c>).</summary>
    public const int StickArrowBottomReverseDotted = 58;

    /// <summary>A central connection at the target (<c>A-&gt;&gt;()B</c>).</summary>
    public const int CentralConnection = 59;

    /// <summary>A central connection at the source (<c>A()-&gt;&gt;B</c>).</summary>
    public const int CentralConnectionReverse = 60;

    /// <summary>Central connections at both ends (<c>A()-&gt;&gt;()B</c>).</summary>
    public const int CentralConnectionDual = 61;

    /// <summary>
    ///     Gets a value indicating whether a line type is a message drawn as an arrow between participants (what the
    ///     document counts as an edge).
    /// </summary>
    /// <param name="type">The line type.</param>
    /// <returns>Whether it is one of the 26 arrows.</returns>
    public static bool IsArrow(int type) => type is
        Solid or Dotted or SolidCross or DottedCross or SolidOpen or DottedOpen or SolidPoint or DottedPoint
        or BidirectionalSolid or BidirectionalDotted or (>= SolidTop and <= StickArrowBottomReverse)
        or (>= SolidTopDotted and <= StickArrowBottomReverseDotted);
}

/// <summary>
///     The placements of a note (Mermaid's <c>PLACEMENT</c>).
/// </summary>
/// <remarks>The class only holds constants and is safe for concurrent use.</remarks>
internal static class SequencePlacement
{
    /// <summary><c>note left of</c>.</summary>
    public const int LeftOf = 0;

    /// <summary><c>note right of</c>.</summary>
    public const int RightOf = 1;

    /// <summary><c>note over</c>.</summary>
    public const int Over = 2;
}

/// <summary>
///     The participant types that Mermaid draws (its <c>PARTICIPANT_TYPE</c>); any other type set by <c>@{ type: ... }</c>
///     is kept as written and draws nothing, as in Mermaid.
/// </summary>
/// <remarks>The class only holds constants and is safe for concurrent use.</remarks>
internal static class SequenceParticipantTypes
{
    /// <summary>A stick figure (the <c>actor</c> keyword).</summary>
    public const string Actor = "actor";

    /// <summary>A circle with a bar.</summary>
    public const string Boundary = "boundary";

    /// <summary>Two stacked boxes.</summary>
    public const string Collections = "collections";

    /// <summary>A circle with an arrow.</summary>
    public const string Control = "control";

    /// <summary>A cylinder.</summary>
    public const string Database = "database";

    /// <summary>A circle with an underline.</summary>
    public const string Entity = "entity";

    /// <summary>A box (the <c>participant</c> keyword and the default).</summary>
    public const string Participant = "participant";

    /// <summary>A horizontal cylinder.</summary>
    public const string Queue = "queue";

    /// <summary>Gets every type Mermaid draws, in Mermaid's order.</summary>
    public static IReadOnlyList<string> All { get; } = [Actor, Boundary, Collections, Control, Database, Entity, Participant, Queue];

    /// <summary>
    ///     Gets a value indicating whether Mermaid draws a type (the comparison is case-sensitive, as Mermaid's).
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>Whether the type is one of <see cref="All"/>.</returns>
    public static bool IsKnown(string type) => All.Contains(type, StringComparer.Ordinal);
}

/// <summary>
///     A text of a statement with its explicit wrap setting: the result of Mermaid's <c>parseMessage</c>.
/// </summary>
/// <param name="Text">The text, trimmed and without its <c>wrap:</c> or <c>nowrap:</c> prefix.</param>
/// <param name="Wrap">True after a <c>wrap:</c> prefix, false after <c>nowrap:</c>, null without one (the configuration decides).</param>
internal readonly record struct SequenceText(string Text, bool? Wrap)
{
    /// <summary>
    ///     Reads a text as Mermaid's <c>parseMessage</c> does: trimmed, then a leading <c>wrap:</c> or <c>nowrap:</c>
    ///     (optionally after a colon, case-sensitive) is taken as the wrap setting and removed, and the rest is trimmed.
    /// </summary>
    /// <param name="text">The text as written.</param>
    /// <returns>The text and its wrap setting.</returns>
    public static SequenceText Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var trimmed = Syntax.Preprocessor.TrimJs(text);
        var start = trimmed.StartsWith(':') ? 1 : 0;
        if (string.CompareOrdinal(trimmed, start, "wrap:", 0, 5) == 0)
        {
            return new SequenceText(Syntax.Preprocessor.TrimJs(trimmed[(start + 5)..]), true);
        }

        if (string.CompareOrdinal(trimmed, start, "nowrap:", 0, 7) == 0)
        {
            return new SequenceText(Syntax.Preprocessor.TrimJs(trimmed[(start + 7)..]), false);
        }

        return new SequenceText(trimmed, null);
    }
}

/// <summary>
///     A participant of the sequence database (Mermaid's <c>Actor</c>).
/// </summary>
/// <remarks>
///     Participants are changed only while the statements are applied; afterwards the model is read only. A participant
///     declared again with a description is replaced by a new one at the same position, as Mermaid replaces its map
///     entry.
/// </remarks>
internal sealed class SequenceActor
{
    /// <summary>
    ///     Initializes a participant.
    /// </summary>
    /// <param name="id">The id (the name as written).</param>
    /// <param name="name">The name.</param>
    /// <param name="description">The description (the label that is drawn).</param>
    /// <param name="wrap">The explicit wrap setting of the description, or null.</param>
    /// <param name="type">The participant type (one of <see cref="SequenceParticipantTypes"/>, or any text from metadata).</param>
    /// <param name="box">The box, or null.</param>
    /// <param name="prevActor">The id of the participant declared before it, or null.</param>
    /// <param name="location">The location of the statement that declared it.</param>
    public SequenceActor(string id, string name, string description, bool? wrap, string type, SequenceBox? box, string? prevActor, MermaidLocation location)
    {
        Id = id;
        Name = name;
        Description = description;
        Wrap = wrap;
        Type = type;
        Box = box;
        PrevActor = prevActor;
        Location = location;
    }

    /// <summary>Gets the id.</summary>
    public string Id { get; }

    /// <summary>Gets the name.</summary>
    public string Name { get; }

    /// <summary>Gets the description, the label that is drawn.</summary>
    public string Description { get; }

    /// <summary>Gets the explicit wrap setting of the description, or null when the configuration decides.</summary>
    public bool? Wrap { get; }

    /// <summary>Gets the participant type.</summary>
    public string Type { get; }

    /// <summary>Gets the box the participant is drawn in, or null.</summary>
    public SequenceBox? Box { get; internal set; }

    /// <summary>Gets the id of the participant declared before this one, or null.</summary>
    public string? PrevActor { get; }

    /// <summary>Gets the id of the participant declared after this one, or null.</summary>
    public string? NextActor { get; internal set; }

    /// <summary>Gets the menu links (<c>link</c> and <c>links</c>): label to URL, as JSON values.</summary>
    public SequencePropertyMap Links { get; } = new();

    /// <summary>Gets the properties (<c>properties</c>), as JSON values.</summary>
    public SequencePropertyMap Properties { get; } = new();

    /// <summary>Gets the location of the statement that declared the participant.</summary>
    public MermaidLocation Location { get; }
}

/// <summary>
///     The numbering of an <c>autonumber</c> statement.
/// </summary>
/// <param name="Start">The first number, or null to continue.</param>
/// <param name="Step">The step, or null to keep the current one.</param>
/// <param name="Visible">Whether the numbers are drawn from here on.</param>
internal sealed record SequenceAutonumber(double? Start, double? Step, bool Visible);

/// <summary>
///     A message of the sequence database (Mermaid's <c>Message</c>): a signal, a note, a block marker, an activation
///     or an autonumber statement, in the order of the text.
/// </summary>
/// <param name="Id">The id: the index of the message, as text.</param>
/// <param name="From">The source participant, or null.</param>
/// <param name="To">The target participant, or null.</param>
/// <param name="Text">The text (empty for markers without one).</param>
/// <param name="Wrap">The explicit wrap setting, or null when the configuration decides.</param>
/// <param name="Type">The line type (<see cref="SequenceLineType"/>).</param>
/// <param name="Location">The location of the statement.</param>
internal sealed record SequenceMessage(string Id, string? From, string? To, string Text, bool? Wrap, int Type, MermaidLocation Location)
{
    /// <summary>Gets whether the message activates its target (<c>+</c> and central connections); null for notes and autonumber statements.</summary>
    public bool? Activate { get; init; }

    /// <summary>Gets the central connection (<see cref="SequenceLineType.CentralConnection"/> and the like, or 0); null for notes and autonumber statements.</summary>
    public int? CentralConnection { get; init; }

    /// <summary>Gets the placement of a note (<see cref="SequencePlacement"/>), or null.</summary>
    public int? Placement { get; init; }

    /// <summary>Gets the numbering of an autonumber statement, or null.</summary>
    public SequenceAutonumber? Autonumber { get; init; }
}

/// <summary>
///     A note of the sequence database.
/// </summary>
/// <param name="From">The first participant.</param>
/// <param name="To">The second participant (the same as the first unless the note spans two).</param>
/// <param name="Placement">The placement (<see cref="SequencePlacement"/>).</param>
/// <param name="Text">The text.</param>
/// <param name="Wrap">The explicit wrap setting, or null.</param>
/// <param name="Location">The location of the statement.</param>
internal sealed record SequenceNote(string From, string To, int Placement, string Text, bool? Wrap, MermaidLocation Location);

/// <summary>
///     The color and title of a <c>box</c> statement, as Mermaid's <c>parseBoxData</c> reads them.
/// </summary>
/// <param name="Text">The title (sanitized), or null.</param>
/// <param name="Color">The fill color as written, or <c>transparent</c>.</param>
/// <param name="Wrap">The explicit wrap setting of the title, or null.</param>
internal sealed record SequenceBoxData(string? Text, string Color, bool? Wrap);

/// <summary>
///     A box of the sequence database: a titled background around participants.
/// </summary>
/// <remarks>The member list is changed only while the statements are applied.</remarks>
internal sealed class SequenceBox
{
    /// <summary>The ids of the participants declared in the box.</summary>
    private readonly List<string> _actorKeys = [];

    /// <summary>
    ///     Initializes a box.
    /// </summary>
    /// <param name="name">The title, or null.</param>
    /// <param name="fill">The fill color as written.</param>
    /// <param name="wrap">The explicit wrap setting of the title, or null.</param>
    /// <param name="location">The location of the statement.</param>
    public SequenceBox(string? name, string fill, bool? wrap, MermaidLocation location)
    {
        Name = name;
        Fill = fill;
        Wrap = wrap;
        Location = location;
    }

    /// <summary>Gets the title, or null.</summary>
    public string? Name { get; }

    /// <summary>Gets the fill color as written (a CSS color, or <c>transparent</c>).</summary>
    public string Fill { get; }

    /// <summary>Gets the explicit wrap setting of the title, or null.</summary>
    public bool? Wrap { get; }

    /// <summary>Gets the ids of the participants declared in the box, in order (an id declared twice is listed twice, as in Mermaid).</summary>
    public IReadOnlyList<string> ActorKeys => _actorKeys;

    /// <summary>Gets the location of the statement.</summary>
    public MermaidLocation Location { get; }

    /// <summary>
    ///     Adds a participant to the box.
    /// </summary>
    /// <param name="id">The participant id.</param>
    internal void AddActorKey(string id) => _actorKeys.Add(id);
}

/// <summary>
///     The properties of a JavaScript object as the sequence database holds them for links and properties: keys with
///     JSON values, enumerated in JavaScript's order (keys that are array indexes first, in numeric order, then the
///     other keys in insertion order).
/// </summary>
/// <remarks>The map is changed only while the statements are applied.</remarks>
internal sealed class SequencePropertyMap
{
    /// <summary>The values by key.</summary>
    private readonly Dictionary<string, JsonNode?> _values = new(StringComparer.Ordinal);

    /// <summary>The keys in insertion order.</summary>
    private readonly List<string> _order = [];

    /// <summary>Gets the number of keys.</summary>
    public int Count => _order.Count;

    /// <summary>
    ///     Gets the keys in JavaScript's enumeration order.
    /// </summary>
    public IEnumerable<string> Keys =>
        _order.Where(IsArrayIndex).OrderBy(key => uint.Parse(key, CultureInfo.InvariantCulture)).Concat(_order.Where(key => !IsArrayIndex(key)));

    /// <summary>
    ///     Gets the value of a key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The JSON value (null for JSON <c>null</c>).</param>
    /// <returns>Whether the key exists.</returns>
    public bool TryGetValue(string key, out JsonNode? value) => _values.TryGetValue(key, out value);

    /// <summary>
    ///     Sets a key; an existing key keeps its position.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    internal void Set(string key, JsonNode? value)
    {
        if (!_values.ContainsKey(key))
        {
            _order.Add(key);
        }

        _values[key] = value;
    }

    /// <summary>
    ///     Gets a value indicating whether a key is a JavaScript array index (a canonical integer below 2^32 - 1).
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>Whether it is.</returns>
    private static bool IsArrayIndex(string key) =>
        key.Length > 0 && key.Length <= 10 && key.All(char.IsAsciiDigit) && (key.Length == 1 || key[0] != '0')
        && ulong.Parse(key, CultureInfo.InvariantCulture) < uint.MaxValue;
}
