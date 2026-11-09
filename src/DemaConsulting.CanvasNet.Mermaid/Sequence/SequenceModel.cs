// Portions derived from mermaid (MIT); see third-party/README.md
using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Sequence;

/// <summary>
///     A parsed sequence diagram: a port of Mermaid's sequence database (<c>sequenceDb.ts</c>), which applies the
///     statements of the parser in order, as Mermaid's <c>apply</c> does once the whole text has parsed.
/// </summary>
/// <remarks>
///     <para>
///         <b>Fidelity.</b> Every database operation is ported with Mermaid's semantics: participants in insertion order
///         with their previous and next participants (a participant declared again with an alias is replaced in place, so
///         it can become its own neighbor, as in Mermaid), their keyword type or the <c>type</c> and <c>alias</c> of their
///         <c>@{ ... }</c> metadata (read as YAML), and their box; boxes with the color and title of
///         <c>parseBoxData</c>; messages with Mermaid's line types and ids; notes; block markers; activations, with the
///         error for deactivating an inactive participant; <c>create</c> and <c>destroy</c> with their errors; the
///         <c>links</c>, <c>link</c> and <c>properties</c> data (JSON, sanitized, a failure ignored as in Mermaid);
///         <c>autonumber</c> statements; and the title and accessibility texts, sanitized as Mermaid's
///         <c>sanitizeText</c> does (<see cref="HtmlSanitizer"/>).
///     </para>
///     <para>
///         <b>Wrapping.</b> Every text keeps its explicit <c>wrap:</c> or <c>nowrap:</c> setting, or null; Mermaid
///         replaces null by its <c>autoWrap</c>, which depends on the configuration, so a render resolves it with
///         <see cref="EffectiveWrap"/>.
///     </para>
///     <para>
///         <b>Errors and limits.</b> Mermaid's database errors are located <see cref="MermaidSyntaxException"/>s with
///         Mermaid's own message (code <see cref="MermaidDiagnosticCodes.SyntaxError"/>, or
///         <see cref="MermaidDiagnosticCodes.YamlInvalid"/> for invalid metadata). Participants count against
///         <see cref="MermaidLimits.MaxNodes"/> (<see cref="MermaidDiagnosticCodes.LimitExceeded"/>); messages are not
///         limited, as Mermaid applies no <c>maxEdges</c> to sequence diagrams. Every step costs constant time apart from
///         the text and JSON work, so applying is linear in the statements.
///     </para>
///     <para>
///         The model is filled by one parser on one thread; once parsing is over it is never changed, so it is safe for
///         concurrent use.
///     </para>
/// </remarks>
internal sealed partial class SequenceModel
{
    /// <summary>The color keywords and system colors that browsers accept as a color besides the named colors.</summary>
    private static readonly FrozenSet<string> ColorKeywords = new[]
    {
        "currentcolor", "inherit", "initial", "unset", "revert", "revert-layer",
        "AccentColor", "AccentColorText", "ActiveText", "ButtonBorder", "ButtonFace", "ButtonText", "Canvas",
        "CanvasText", "Field", "FieldText", "GrayText", "Highlight", "HighlightText", "LinkText", "Mark", "MarkText",
        "SelectedItem", "SelectedItemText", "VisitedText", "ActiveBorder", "ActiveCaption", "AppWorkspace", "Background",
        "ButtonHighlight", "ButtonShadow", "CaptionText", "InactiveBorder", "InactiveCaption", "InactiveCaptionText",
        "InfoBackground", "InfoText", "Menu", "MenuText", "Scrollbar", "ThreeDDarkShadow", "ThreeDFace",
        "ThreeDHighlight", "ThreeDLightShadow", "ThreeDShadow", "Window", "WindowFrame", "WindowText",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>The limits of the open call.</summary>
    private readonly MermaidLimits _limits;

    /// <summary>The participants in insertion order.</summary>
    private readonly List<SequenceActor> _actors = [];

    /// <summary>The index of each participant in <see cref="_actors"/>.</summary>
    private readonly Dictionary<string, int> _actorIndex = new(StringComparer.Ordinal);

    /// <summary>The messages in order.</summary>
    private readonly List<SequenceMessage> _messages = [];

    /// <summary>The notes in order.</summary>
    private readonly List<SequenceNote> _notes = [];

    /// <summary>The boxes in order.</summary>
    private readonly List<SequenceBox> _boxes = [];

    /// <summary>The index of the first message after each <c>create</c>, by participant.</summary>
    private readonly Dictionary<string, int> _createdActors = new(StringComparer.Ordinal);

    /// <summary>The index of the first message after each <c>destroy</c>, by participant.</summary>
    private readonly Dictionary<string, int> _destroyedActors = new(StringComparer.Ordinal);

    /// <summary>The activations minus the deactivations of each participant (Mermaid's <c>activationCount</c>, kept up to date).</summary>
    private readonly Dictionary<string, int> _activations = new(StringComparer.Ordinal);

    /// <summary>The participant declared last (Mermaid's <c>prevActor</c>).</summary>
    private string? _prevActor;

    /// <summary>The box being declared (Mermaid's <c>currentBox</c>).</summary>
    private SequenceBox? _currentBox;

    /// <summary>The participant created last that has no creating message yet (Mermaid's <c>lastCreated</c>).</summary>
    private string? _lastCreated;

    /// <summary>The participant destroyed last that has no destroying message yet (Mermaid's <c>lastDestroyed</c>).</summary>
    private string? _lastDestroyed;

    /// <summary>
    ///     Initializes an empty model.
    /// </summary>
    /// <param name="limits">The limits of the open call.</param>
    /// <param name="keywordLocation">The location of the diagram keyword.</param>
    public SequenceModel(MermaidLimits limits, MermaidLocation keywordLocation)
    {
        ArgumentNullException.ThrowIfNull(limits);
        _limits = limits;
        KeywordLocation = keywordLocation;
    }

    /// <summary>Gets the location of the diagram keyword.</summary>
    public MermaidLocation KeywordLocation { get; }

    /// <summary>Gets the participants in insertion order (Mermaid's <c>getActors</c> map and <c>getActorKeys</c>).</summary>
    public IReadOnlyList<SequenceActor> Actors => _actors;

    /// <summary>Gets the messages in order: signals, notes, block markers, activations and autonumber statements.</summary>
    public IReadOnlyList<SequenceMessage> Messages => _messages;

    /// <summary>Gets the notes in order.</summary>
    public IReadOnlyList<SequenceNote> Notes => _notes;

    /// <summary>Gets the boxes in order.</summary>
    public IReadOnlyList<SequenceBox> Boxes => _boxes;

    /// <summary>Gets, for each created participant, the index of the first message after its <c>create</c>.</summary>
    public IReadOnlyDictionary<string, int> CreatedActors => _createdActors;

    /// <summary>Gets, for each destroyed participant, the index of the first message after its <c>destroy</c>.</summary>
    public IReadOnlyDictionary<string, int> DestroyedActors => _destroyedActors;

    /// <summary>Gets the title of the <c>title</c> statement, sanitized (empty when there is none).</summary>
    public string DiagramTitle { get; private set; } = string.Empty;

    /// <summary>Gets the accessible title (empty when there is none).</summary>
    public string AccessibleTitle { get; private set; } = string.Empty;

    /// <summary>Gets the accessible description (empty when there is none).</summary>
    public string AccessibleDescription { get; private set; } = string.Empty;

    /// <summary>Gets the title the diagram declares, or null (the frontmatter title is the document's fallback).</summary>
    public string? Title => DiagramTitle.Length > 0 ? DiagramTitle : null;

    /// <summary>Gets the accessible title, or null.</summary>
    public string? AccTitle => AccessibleTitle.Length > 0 ? AccessibleTitle : null;

    /// <summary>Gets the number of participants, declared, mentioned and created.</summary>
    public int NodeCount => _actors.Count;

    /// <summary>Gets the number of messages drawn as arrows (notes, blocks and activations not included).</summary>
    public int EdgeCount => _messages.Count(message => SequenceLineType.IsArrow(message.Type));

    /// <summary>Gets the number of boxes.</summary>
    public int SubgraphCount => _boxes.Count;

    /// <summary>Gets a value indicating whether the diagram has a box (Mermaid's <c>hasAtLeastOneBox</c>).</summary>
    public bool HasAtLeastOneBox => _boxes.Count > 0;

    /// <summary>Gets a value indicating whether a box has a title (Mermaid's <c>hasAtLeastOneBoxWithTitle</c>).</summary>
    public bool HasAtLeastOneBoxWithTitle => _boxes.Exists(box => !string.IsNullOrEmpty(box.Name));

    /// <summary>
    ///     Gets the wrap setting of a text under a configuration: its explicit setting, or else the configuration's
    ///     (Mermaid's <c>autoWrap</c>).
    /// </summary>
    /// <param name="wrap">The explicit setting of the text, or null.</param>
    /// <param name="config">The sequence configuration of the render.</param>
    /// <returns>Whether the text wraps.</returns>
    public static bool EffectiveWrap(bool? wrap, SequenceConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return wrap ?? config.Wrap;
    }

    /// <summary>
    ///     Gets a participant by id.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <returns>The participant, or null.</returns>
    public SequenceActor? GetActor(string id) => _actorIndex.TryGetValue(id, out var index) ? _actors[index] : null;

    /// <summary>
    ///     Reads the color and title of a <c>box</c> statement as Mermaid's <c>parseBoxData</c> does: a leading
    ///     <c>rgb()</c>, <c>rgba()</c>, <c>hsl()</c> or <c>hsla()</c> (up to the last closing parenthesis), or else a leading
    ///     word, is the color when a browser accepts it as one; otherwise the color is <c>transparent</c> and the whole
    ///     text is the title. The title may have a <c>wrap:</c> or <c>nowrap:</c> prefix and is sanitized.
    /// </summary>
    /// <param name="text">The text after the keyword.</param>
    /// <returns>The box data.</returns>
    public static SequenceBoxData ParseBoxData(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        // /^((?:rgba?|hsla?)\s*\(.*\)|\w*)(.*)$/: a failed match gives no color and no title
        var color = "transparent";
        string? title = null;
        if (MatchBoxData(text, out var colorText, out var rest))
        {
            color = colorText.Length > 0 ? Preprocessor.TrimJs(colorText) : "transparent";
            title = rest.Length > 0 ? Preprocessor.TrimJs(rest) : null;
        }

        // A text the browser does not take as a color is part of the title
        if (!IsBrowserColor(color))
        {
            color = "transparent";
            title = Preprocessor.TrimJs(text);
        }

        var parsed = title is null ? (SequenceText?)null : SequenceText.Parse(title);
        var name = parsed is { Text.Length: > 0 } value ? HtmlSanitizer.Sanitize(value.Text) : null;
        return new SequenceBoxData(name, color, parsed?.Wrap);
    }

    /// <summary>
    ///     Sets the diagram title (Mermaid's <c>setDiagramTitle</c>: sanitized, not trimmed).
    /// </summary>
    /// <param name="text">The title.</param>
    public void SetDiagramTitle(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        DiagramTitle = HtmlSanitizer.Sanitize(text);
    }

    /// <summary>
    ///     Sets the accessible title (Mermaid's <c>setAccTitle</c>: sanitized, leading white space removed).
    /// </summary>
    /// <param name="text">The title.</param>
    public void SetAccessibleTitle(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var sanitized = HtmlSanitizer.Sanitize(text);
        var start = 0;
        while (start < sanitized.Length && ScanPatterns.IsJsWhitespace(sanitized[start]))
        {
            start++;
        }

        AccessibleTitle = sanitized[start..];
    }

    /// <summary>
    ///     Sets the accessible description (Mermaid's <c>setAccDescription</c>: sanitized, the white space after each line
    ///     feed removed).
    /// </summary>
    /// <param name="text">The description.</param>
    public void SetAccessibleDescription(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var sanitized = HtmlSanitizer.Sanitize(text);
        var builder = new StringBuilder(sanitized.Length);
        var i = 0;
        while (i < sanitized.Length)
        {
            builder.Append(sanitized[i]);
            i++;

            // \n\s+ becomes \n: skip the white space after a line feed (more line feeds included)
            if (sanitized[i - 1] == '\n')
            {
                while (i < sanitized.Length && ScanPatterns.IsJsWhitespace(sanitized[i]))
                {
                    i++;
                }
            }
        }

        AccessibleDescription = builder.ToString();
    }

    /// <summary>
    ///     Applies the statements of a parsed text in order (Mermaid's <c>apply</c>).
    /// </summary>
    /// <param name="statements">The statements.</param>
    /// <exception cref="MermaidSyntaxException">Thrown, located, for an error of Mermaid's database, invalid metadata or a limit.</exception>
    public void Apply(IEnumerable<SequenceStatement> statements)
    {
        ArgumentNullException.ThrowIfNull(statements);
        foreach (var statement in statements)
        {
            Apply(statement);
        }
    }

    /// <summary>
    ///     Applies one statement.
    /// </summary>
    /// <param name="statement">The statement.</param>
    private void Apply(SequenceStatement statement)
    {
        switch (statement)
        {
            case SequenceParticipantStatement { Create: true } create:
                ApplyCreate(create);
                break;
            case SequenceParticipantStatement participant:
                AddActor(participant.Actor, participant.Description, participant.Draw, participant.Config, participant.Location);
                break;
            case SequenceDestroyStatement destroy:
                _lastDestroyed = destroy.Actor;
                _destroyedActors[destroy.Actor] = _messages.Count;
                break;
            case SequenceActorSignalStatement signal:
                AddSignal(signal.Actor, null, null, signal.Type, false, 0, signal.Location);
                break;
            case SequenceMessageStatement message:
                ApplyMessage(message);
                break;
            case SequenceNoteStatement note:
                AddNote(note);
                break;
            case SequenceActorDataStatement data:
                ApplyActorData(data);
                break;
            case SequenceBoxStartStatement box:
                _currentBox = new SequenceBox(box.Data.Text, box.Data.Color, box.Data.Wrap, box.Location);
                _boxes.Add(_currentBox);
                break;
            case SequenceBoxEndStatement:
                _currentBox = null;
                break;
            case SequenceBlockStatement block:
                AddSignal(null, null, block.Text, block.Type, false, 0, block.Location);
                break;
            case SequenceAutonumberStatement autonumber:
                _messages.Add(new SequenceMessage(NextId(), null, null, string.Empty, false, SequenceLineType.Autonumber, autonumber.Location)
                {
                    Autonumber = new SequenceAutonumber(autonumber.Start, autonumber.Step, autonumber.Visible),
                });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(statement), statement.GetType().Name, "Unknown sequence statement.");
        }
    }

    /// <summary>
    ///     Applies a <c>create</c> (Mermaid's <c>createParticipant</c>): an existing id is an error; the participant is
    ///     added and the next message must create it.
    /// </summary>
    /// <param name="create">The statement.</param>
    private void ApplyCreate(SequenceParticipantStatement create)
    {
        if (_actorIndex.ContainsKey(create.Actor))
        {
            throw DatabaseError(
                "It is not possible to have actors with the same id, even if one is destroyed before the next is created. Use 'AS' aliases to simulate the behavior",
                create.Location,
                $"'{create.Actor}' exists already; create a participant with another id and an alias, for example 'create participant {create.Actor}2 as {create.Actor}'");
        }

        _lastCreated = create.Actor;
        AddActor(create.Actor, create.Description, create.Draw, create.Config, create.Location);
        _createdActors[create.Actor] = _messages.Count;
    }

    /// <summary>
    ///     Applies a message (Mermaid's <c>addMessage</c> case): the message after a <c>create</c> must go to the created
    ///     participant, and the message after a <c>destroy</c> must come from or go to the destroyed one.
    /// </summary>
    /// <param name="message">The statement.</param>
    private void ApplyMessage(SequenceMessageStatement message)
    {
        // Mermaid's messages name the participant as "undefined" (it reads .name of the id); the hint names it
        if (_lastCreated is { } created)
        {
            if (message.To != created)
            {
                throw DatabaseError(
                    "The created participant undefined does not have an associated creating message after its declaration. Please check the sequence diagram.",
                    message.Location,
                    $"send a message to '{created}' right after 'create participant {created}'");
            }

            _lastCreated = null;
        }
        else if (_lastDestroyed is { } destroyed)
        {
            if (message.To != destroyed && message.From != destroyed)
            {
                throw DatabaseError(
                    "The destroyed participant undefined does not have an associated destroying message after its declaration. Please check the sequence diagram.",
                    message.Location,
                    $"send a message from or to '{destroyed}' right after 'destroy {destroyed}'");
            }

            _lastDestroyed = null;
        }

        AddSignal(message.From, message.To, message.Text, message.Type, message.Activate, message.CentralConnection, message.Location);
    }

    /// <summary>
    ///     Adds or replaces a participant (Mermaid's <c>addActor</c>).
    /// </summary>
    /// <param name="id">The id (also the name).</param>
    /// <param name="description">The alias, or null.</param>
    /// <param name="type">The keyword type, or null for a mention.</param>
    /// <param name="metadata">The <c>@{ ... }</c> metadata, or null.</param>
    /// <param name="location">The location of the statement.</param>
    private void AddActor(string id, SequenceText? description, string? type, SequenceMetadataText? metadata, MermaidLocation location)
    {
        var name = id;
        var assignedBox = _currentBox;

        // The metadata is YAML: one line is wrapped as a flow mapping; its type and alias override the statement's
        var doc = metadata is null ? null : ReadMetadata(metadata) as YamlMapping;
        if (doc?.Get("type") is { } docType and not YamlScalar { Value: null })
        {
            type = docType.ToJsString();
        }

        if (doc?.Get("alias") is { IsTruthy: true } alias && (description is null || description.Value.Text == name))
        {
            description = new SequenceText(alias.ToJsString(), description?.Wrap);
        }

        // A participant declared again keeps its box; a mention without an alias changes nothing else
        var exists = _actorIndex.TryGetValue(id, out var index);
        if (exists)
        {
            var old = _actors[index];
            if (_currentBox is not null && old.Box is not null && !ReferenceEquals(_currentBox, old.Box))
            {
                throw DatabaseError(
                    $"A same participant should only be defined in one Box: {old.Name} can't be in '{old.Box.Name ?? "undefined"}' and in '{_currentBox.Name ?? "undefined"}' at the same time.",
                    location,
                    $"declare '{old.Name}' in one box only");
            }

            assignedBox = old.Box ?? _currentBox;
            old.Box = assignedBox;
            if (description is null)
            {
                return;
            }
        }
        else if (_actors.Count >= _limits.MaxNodes)
        {
            throw new MermaidSyntaxException(
                MermaidDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"the diagram has more than {_limits.MaxNodes} participants (MermaidLimits.MaxNodes)"),
                location,
                "split the diagram, or raise MermaidLimits.MaxNodes when the source is trusted");
        }

        // Without an alias, or without a type, the description is the name
        if (description is null || type is null)
        {
            description = new SequenceText(name, null);
        }

        var actor = new SequenceActor(id, name, description.Value.Text, description.Value.Wrap, type ?? SequenceParticipantTypes.Participant, assignedBox, _prevActor, location);
        if (exists)
        {
            _actors[index] = actor;
        }
        else
        {
            _actorIndex[id] = _actors.Count;
            _actors.Add(actor);
        }

        if (_prevActor is not null && GetActor(_prevActor) is { } previous)
        {
            previous.NextActor = id;
        }

        _currentBox?.AddActorKey(id);
        _prevActor = id;
    }

    /// <summary>
    ///     Reads the YAML of participant metadata, as Mermaid does with js-yaml's <c>JSON_SCHEMA</c>.
    /// </summary>
    /// <param name="metadata">The metadata.</param>
    /// <returns>The value of the YAML document.</returns>
    private YamlNode? ReadMetadata(SequenceMetadataText metadata)
    {
        var multiLine = metadata.Text.Contains('\n', StringComparison.Ordinal);
        var yaml = multiLine ? metadata.Text + "\n" : "{\n" + metadata.Text + "\n}";
        var offset = multiLine ? 0 : 2;
        return YamlSubsetParser.Parse(
            yaml,
            position => metadata.Locate(Math.Clamp(position - offset, 0, metadata.Text.Length)),
            _limits.MaxYamlDepth);
    }

    /// <summary>
    ///     Adds a signal (Mermaid's <c>addSignal</c>): a message, a block marker, an activation or a central connection.
    /// </summary>
    /// <param name="from">The source participant, or null.</param>
    /// <param name="to">The target participant, or null.</param>
    /// <param name="text">The text, or null.</param>
    /// <param name="type">The line type.</param>
    /// <param name="activate">Whether the message activates its target.</param>
    /// <param name="centralConnection">The central connection, or 0.</param>
    /// <param name="location">The location of the statement.</param>
    private void AddSignal(string? from, string? to, SequenceText? text, int type, bool activate, int centralConnection, MermaidLocation location)
    {
        // Deactivating a participant that is not active is an error
        var participant = from ?? string.Empty;
        if (type == SequenceLineType.ActiveEnd && _activations.GetValueOrDefault(participant) < 1)
        {
            throw DatabaseError(
                $"Trying to inactivate an inactive participant ({from})",
                location,
                $"activate '{from}' (with 'activate {from}' or a '+' arrow) before it is deactivated");
        }

        if (type is SequenceLineType.ActiveStart or SequenceLineType.ActiveEnd && participant.Length > 0)
        {
            _activations[participant] = _activations.GetValueOrDefault(participant) + (type == SequenceLineType.ActiveStart ? 1 : -1);
        }

        _messages.Add(new SequenceMessage(NextId(), from, to, text?.Text ?? string.Empty, text?.Wrap, type, location)
        {
            Activate = activate,
            CentralConnection = centralConnection,
        });
    }

    /// <summary>
    ///     Adds a note (Mermaid's <c>addNote</c>), which is also a message.
    /// </summary>
    /// <param name="note">The statement.</param>
    private void AddNote(SequenceNoteStatement note)
    {
        _notes.Add(new SequenceNote(note.From, note.To, note.Placement, note.Text.Text, note.Text.Wrap, note.Location));
        _messages.Add(new SequenceMessage(NextId(), note.From, note.To, note.Text.Text, note.Text.Wrap, SequenceLineType.Note, note.Location)
        {
            Placement = note.Placement,
        });
    }

    /// <summary>
    ///     Applies a <c>links</c>, <c>link</c>, <c>properties</c> or <c>details</c> statement (Mermaid's <c>addLinks</c>,
    ///     <c>addALink</c>, <c>addProperties</c> and <c>addDetails</c>): text that does not parse is ignored, as Mermaid
    ///     only logs it, and <c>details</c> reads a page element that does not exist outside a browser page.
    /// </summary>
    /// <param name="data">The statement.</param>
    private void ApplyActorData(SequenceActorDataStatement data)
    {
        var actor = GetActor(data.Actor);
        if (actor is null)
        {
            return;
        }

        var sanitized = HtmlSanitizer.Sanitize(data.Text.Text);
        switch (data.Kind)
        {
            case SequenceActorDataKind.Links:
                if (TryParseJson(sanitized.Replace("&equals;", "=", StringComparison.Ordinal).Replace("&amp;", "&", StringComparison.Ordinal), out var links))
                {
                    AssignProperties(actor.Links, links);
                }

                break;
            case SequenceActorDataKind.Link:
                // The separator is found before the entities are decoded, as in Mermaid
                var separator = sanitized.IndexOf('@', StringComparison.Ordinal);
                var decoded = sanitized.Replace("&equals;", "=", StringComparison.Ordinal).Replace("&amp;", "&", StringComparison.Ordinal);
                var label = Preprocessor.TrimJs(JsSlice(decoded, 0, separator - 1));
                var url = Preprocessor.TrimJs(JsSlice(decoded, separator + 1, decoded.Length));
                actor.Links.Set(label, JsonValue.Create(url));
                break;
            case SequenceActorDataKind.Properties:
                if (TryParseJson(sanitized, out var properties))
                {
                    AssignProperties(actor.Properties, properties);
                }

                break;
            default:
                break;
        }
    }

    /// <summary>
    ///     Parses JSON as JavaScript's <c>JSON.parse</c> does (a repeated key keeps its first position and its last value).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="value">The parsed value.</param>
    /// <returns>Whether the text is JSON.</returns>
    private static bool TryParseJson(string text, out JsonElement value)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            value = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            value = default;
            return false;
        }
    }

    /// <summary>
    ///     Copies the enumerable properties of a parsed JSON value into a property map, as a JavaScript <c>for...in</c>
    ///     loop does: the keys of an object, the indexes of an array, the indexes of the characters of a string, and
    ///     nothing for other values.
    /// </summary>
    /// <param name="target">The map.</param>
    /// <param name="value">The value.</param>
    private static void AssignProperties(SequencePropertyMap target, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in value.EnumerateObject())
                {
                    target.Set(property.Name, ToNode(property.Value));
                }

                break;
            case JsonValueKind.Array:
                var i = 0;
                foreach (var item in value.EnumerateArray())
                {
                    target.Set(i++.ToString(CultureInfo.InvariantCulture), ToNode(item));
                }

                break;
            case JsonValueKind.String:
                var text = value.GetString()!;
                for (var c = 0; c < text.Length; c++)
                {
                    target.Set(c.ToString(CultureInfo.InvariantCulture), JsonValue.Create(text[c].ToString()));
                }

                break;
            default:
                break;
        }
    }

    /// <summary>
    ///     Converts a JSON element to a node without a parent; numbers become doubles, as in JavaScript.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <returns>The node, or null for JSON <c>null</c>.</returns>
    private static JsonNode? ToNode(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var obj = new JsonObject();
                foreach (var property in element.EnumerateObject())
                {
                    obj[property.Name] = ToNode(property.Value);
                }

                return obj;
            case JsonValueKind.Array:
                return new JsonArray([.. element.EnumerateArray().Select(ToNode)]);
            case JsonValueKind.String:
                return JsonValue.Create(element.GetString());
            case JsonValueKind.Number:
                return JsonValue.Create(double.Parse(element.GetRawText(), NumberStyles.Float, CultureInfo.InvariantCulture));
            case JsonValueKind.True:
                return JsonValue.Create(true);
            case JsonValueKind.False:
                return JsonValue.Create(false);
            default:
                return null;
        }
    }

    /// <summary>
    ///     Takes part of a text as JavaScript's <c>String.prototype.slice</c> does (a negative index counts from the end).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The start.</param>
    /// <param name="end">The end (exclusive).</param>
    /// <returns>The part.</returns>
    private static string JsSlice(string text, int start, int end)
    {
        static int Clamp(int index, int length) => index < 0 ? Math.Max(length + index, 0) : Math.Min(index, length);
        var from = Clamp(start, text.Length);
        var to = Clamp(end, text.Length);
        return to > from ? text[from..to] : string.Empty;
    }

    /// <summary>
    ///     Matches <c>/^((?:rgba?|hsla?)\s*\(.*\)|\w*)(.*)$/</c> (case-sensitive; <c>.</c> does not match a line
    ///     terminator) as JavaScript does.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="color">The first group.</param>
    /// <param name="rest">The second group.</param>
    /// <returns>Whether the text matches.</returns>
    private static bool MatchBoxData(string text, out string color, out string rest)
    {
        // A color function: its name, optional white space, then everything up to the last closing parenthesis
        foreach (var function in (string[])["rgba", "rgb", "hsla", "hsl"])
        {
            if (!text.StartsWith(function, StringComparison.Ordinal))
            {
                continue;
            }

            var open = function.Length;
            while (open < text.Length && ScanPatterns.IsJsWhitespace(text[open]))
            {
                open++;
            }

            var tail = open + 1;
            if (open < text.Length && text[open] == '(' && !HasLineTerminator(text, tail))
            {
                var close = text.LastIndexOf(')');
                if (close >= tail)
                {
                    color = text[..(close + 1)];
                    rest = text[(close + 1)..];
                    return true;
                }
            }
        }

        // Otherwise a word, then the rest; a line terminator in the rest fails the whole match
        var word = 0;
        while (word < text.Length && ScanPatterns.IsJsWordChar(text[word]))
        {
            word++;
        }

        color = text[..word];
        rest = text[word..];
        return !HasLineTerminator(text, word);
    }

    /// <summary>
    ///     Gets a value indicating whether a text has a JavaScript line terminator from a position on.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The position.</param>
    /// <returns>Whether it has.</returns>
    private static bool HasLineTerminator(string text, int start)
    {
        for (var i = start; i < text.Length; i++)
        {
            if (ScanPatterns.IsJsLineTerminator(text[i]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Gets a value indicating whether a browser takes a text as a color (Mermaid checks with
    ///     <c>CSS.supports('color', text)</c>): a named color, <c>transparent</c>, a color keyword or system color, or an
    ///     <c>rgb()</c>, <c>rgba()</c>, <c>hsl()</c> or <c>hsla()</c> function written without a space before its
    ///     parenthesis.
    /// </summary>
    /// <param name="color">The candidate, trimmed.</param>
    /// <returns>Whether it is a color.</returns>
    private static bool IsBrowserColor(string color)
    {
        var open = color.IndexOf('(', StringComparison.Ordinal);
        if (open < 0)
        {
            return ColorKeywords.Contains(color) || CssColor.TryParse(color, out _);
        }

        return color[..open].All(char.IsAsciiLetter) && CssColor.TryParse(color, out _);
    }

    /// <summary>
    ///     Gets the id of the next message: its index, as text.
    /// </summary>
    /// <returns>The id.</returns>
    private string NextId() => _messages.Count.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    ///     Creates the exception for an error of Mermaid's database.
    /// </summary>
    /// <param name="message">Mermaid's message.</param>
    /// <param name="location">The location of the statement.</param>
    /// <param name="hint">The suggested fix.</param>
    /// <returns>The located exception.</returns>
    private static MermaidSyntaxException DatabaseError(string message, MermaidLocation location, string hint) =>
        new(MermaidDiagnosticCodes.SyntaxError, message, location, hint);
}
