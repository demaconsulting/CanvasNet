// Portions derived from PlantUML (MIT); see third-party/README.md
using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;
using DemaConsulting.CanvasNet.PlantUml.Syntax;

namespace DemaConsulting.CanvasNet.PlantUml.Sequence;

/// <summary>
///     Reads the statements of a sequence diagram into a <see cref="SequenceModel"/>.
/// </summary>
/// <remarks>
///     <para>
///         The parser reads PlantUML's sequence statements one by one, by their leading keywords and arrows, without
///         regular expressions: participant declarations (eight kinds, with <c>as</c>, quoted names, <c>order</c> and a
///         color), messages with their arrows, activation shorthands and labels, <c>activate</c>, <c>deactivate</c>,
///         <c>destroy</c>, <c>return</c>, <c>autonumber</c>, notes (on one line or up to <c>end note</c>), groups,
///         dividers, delays, spaces, boxes, page texts, <c>hide footbox</c>, <c>skinparam</c>, and <c>scale</c>.
///     </para>
///     <para>
///         A statement PlantUML accepts but this version does not draw (<c>ref over</c>, <c>newpage</c>,
///         <c>create</c>, gates such as <c>[-&gt;</c>, stereotypes, ...) is reported as
///         <see cref="PlantUmlDiagnosticCodes.StatementUnsupported"/> and skipped (or drawn without the unsupported
///         part). A statement that is not valid PlantUML is a <see cref="PlantUmlSyntaxException"/> with
///         <see cref="PlantUmlDiagnosticCodes.SyntaxError"/> located at it. The participants, messages, label lengths and
///         group nesting are bounded by <see cref="PlantUmlLimits"/> (<see cref="PlantUmlDiagnosticCodes.LimitExceeded"/>).
///     </para>
///     <para>
///         An instance reads one diagram on one thread; <see cref="Parse"/> is thread-safe.
///     </para>
/// </remarks>
internal sealed partial class SequenceParser
{
    /// <summary>The participant keywords with their kinds.</summary>
    private static readonly Dictionary<string, ParticipantKind> ParticipantKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["participant"] = ParticipantKind.Participant,
        ["actor"] = ParticipantKind.Actor,
        ["boundary"] = ParticipantKind.Boundary,
        ["control"] = ParticipantKind.Control,
        ["entity"] = ParticipantKind.Entity,
        ["database"] = ParticipantKind.Database,
        ["collections"] = ParticipantKind.Collections,
        ["queue"] = ParticipantKind.Queue,
    };

    /// <summary>The group keywords.</summary>
    private static readonly HashSet<string> GroupKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "alt", "opt", "loop", "par", "break", "critical", "group",
    };

    /// <summary>The keywords of statements PlantUML accepts that this version does not draw, with what is not drawn.</summary>
    private static readonly Dictionary<string, string> UnsupportedKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["newpage"] = "page breaks (newpage) are not drawn; the diagram is one page",
        ["create"] = "participant creation (create) is not drawn; the participant is drawn at the top",
        ["mainframe"] = "the main frame (mainframe) is not drawn",
        ["autoactivate"] = "automatic activation (autoactivate) is not drawn",
        ["ignore"] = "'ignore newpage' is not drawn",
        ["skin"] = "'skin' is not drawn; the default look is drawn",
        ["also"] = "'also' is not drawn",
        ["par2"] = "par2 groups are not drawn",
    };

    /// <summary>The statements of the diagram.</summary>
    private readonly IReadOnlyList<SourceStatement> _statements;

    /// <summary>The limits of the open call.</summary>
    private readonly PlantUmlLimits _limits;

    /// <summary>The reports found while reading.</summary>
    private readonly List<PlantUmlPendingReport> _reports = [];

    /// <summary>The skin parameters in order.</summary>
    private readonly List<SkinParamEntry> _skin = [];

    /// <summary>The participants in declaration order.</summary>
    private readonly List<SequenceParticipant> _participants = [];

    /// <summary>The participants by code.</summary>
    private readonly Dictionary<string, int> _byCode = new(StringComparer.Ordinal);

    /// <summary>The events in order.</summary>
    private readonly List<SequenceEvent> _events = [];

    /// <summary>The life changes attached to messages, by event index.</summary>
    private readonly Dictionary<int, List<LifeChange>> _changes = [];

    /// <summary>The boxes: title, fill, members, location.</summary>
    private readonly List<(string? Title, Rgba32? Fill, List<int> Members, PlantUmlLocation Location)> _boxes = [];

    /// <summary>The event indexes of the messages that started the open activations, innermost last.</summary>
    private readonly List<int> _activating = [];

    /// <summary>The open groups (keyword and location), innermost last.</summary>
    private readonly List<(string Keyword, PlantUmlLocation Location)> _groups = [];

    /// <summary>The index of the open box, or -1.</summary>
    private int _openBox = -1;

    /// <summary>The event index of the last message that later life changes may attach to, or -1.</summary>
    private int _lastMessage = -1;

    /// <summary>The page texts read so far.</summary>
    private PageTexts _page = PageTexts.None;

    /// <summary>Whether the foot box is hidden.</summary>
    private bool _hideFootbox;

    /// <summary>The diagram's own scale.</summary>
    private double _scale = 1;

    /// <summary>
    ///     Initializes a parser.
    /// </summary>
    /// <param name="statements">The statements.</param>
    /// <param name="limits">The limits.</param>
    private SequenceParser(IReadOnlyList<SourceStatement> statements, PlantUmlLimits limits)
    {
        _statements = statements;
        _limits = limits;
    }

    /// <summary>
    ///     Reads a sequence diagram.
    /// </summary>
    /// <param name="source">The preprocessed source.</param>
    /// <param name="detected">The detected type (sequence).</param>
    /// <param name="limits">The limits of the open call.</param>
    /// <returns>The model.</returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown, located, when a statement is not valid or a limit is exceeded.</exception>
    public static SequenceModel Parse(PreprocessedSource source, DetectedDiagram detected, PlantUmlLimits limits)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(detected);
        ArgumentNullException.ThrowIfNull(limits);
        var parser = new SequenceParser(source.Block.Statements, limits);
        parser.ReadAll();
        return parser.Build(detected.Location);
    }

    /// <summary>
    ///     Reads every statement.
    /// </summary>
    private void ReadAll()
    {
        var i = 0;
        while (i < _statements.Count)
        {
            var statement = _statements[i];
            if (statement.Text.Length > 0)
            {
                i = ReadStatement(i);
            }

            i++;
        }

        if (_groups.Count > 0)
        {
            var (keyword, location) = _groups[^1];
            throw Error($"the group '{keyword}' is not closed", location, "close the group with 'end'");
        }

        if (_openBox >= 0)
        {
            throw Error("the box is not closed", _boxes[_openBox].Location, "close the box with 'end box'");
        }
    }

    /// <summary>
    ///     Reads one statement (and the lines of its block).
    /// </summary>
    /// <param name="index">The index of the statement.</param>
    /// <returns>The index of the last statement read.</returns>
    private int ReadStatement(int index)
    {
        var statement = _statements[index];
        var text = statement.Text;
        if (text.StartsWith('&'))
        {
            // A parallel statement (& A -> B) is drawn after the one before it
            Unsupported("parallel statements (&) are not drawn side by side; the statement is drawn on its own row", statement.Location);
            var start = 1;
            while (start < text.Length && char.IsWhiteSpace(text[start]))
            {
                start++;
            }

            return ReadParallel(index, statement.Slice(start, text.Length - start));
        }

        if (SkinParams.TryRead(_statements, ref index, _skin) || SkinParams.TrySkipStyle(_statements, ref index, _reports))
        {
            return index;
        }

        var word = DiagramDetector.FirstWord(text);
        var lower = word.ToLowerInvariant();
        if (TryReadSeparator(statement))
        {
            return index;
        }

        if (ParticipantKinds.TryGetValue(word, out var kind) && IsDeclaration(text, word))
        {
            ReadDeclaration(statement, kind, word.Length);
            return index;
        }

        if (IsMessage(text, lower))
        {
            ReadMessage(statement);
            return index;
        }

        return ReadKeywordStatement(index, statement, lower);
    }

    /// <summary>
    ///     Reads the statement after the <c>&amp;</c> of a parallel statement.
    /// </summary>
    /// <param name="index">The index of the statement.</param>
    /// <param name="rest">The statement without its <c>&amp;</c>.</param>
    /// <returns>The index of the last statement read.</returns>
    private int ReadParallel(int index, SourceStatement rest)
    {
        if (rest.Text.Length == 0 || rest.Text.StartsWith('&'))
        {
            throw Error("'&' must be followed by a statement", rest.Location, "write for example '& A -> B : text'");
        }

        var word = DiagramDetector.FirstWord(rest.Text);
        if (IsMessage(rest.Text, word.ToLowerInvariant()))
        {
            ReadMessage(rest);
            return index;
        }

        return ReadKeywordStatement(index, rest, word.ToLowerInvariant());
    }

    /// <summary>
    ///     Reads a statement that starts with a keyword (other than a participant declaration).
    /// </summary>
    /// <param name="index">The index of the statement.</param>
    /// <param name="statement">The statement.</param>
    /// <param name="word">The first word in lower case.</param>
    /// <returns>The index of the last statement read.</returns>
    private int ReadKeywordStatement(int index, SourceStatement statement, string word)
    {
        var text = statement.Text;
        switch (word)
        {
            case "note" or "hnote" or "rnote":
                return ReadNote(index);
            case "title" or "header" or "footer" or "caption" or "legend" or "left" or "right" or "center":
                return ReadPageText(index, word);
            case "activate" or "deactivate" or "destroy":
                ReadLifeStatement(statement, word);
                return index;
            case "return":
                ReadReturn(statement);
                return index;
            case "autonumber":
                ReadAutonumber(statement);
                return index;
            case "box":
                ReadBox(statement);
                return index;
            case "else":
                ReadElse(statement);
                return index;
            case "end":
                ReadEnd(statement);
                return index;
            case "endbox":
                CloseBox(statement);
                return index;
            case "hide" or "show":
                ReadHideShow(statement, word);
                return index;
            case "scale":
                ReadScale(statement);
                return index;
            case "ref":
                return SkipRef(index);
        }

        if (GroupKeywords.Contains(word))
        {
            ReadGroup(statement, word);
            return index;
        }

        if (UnsupportedKeywords.TryGetValue(word, out var what))
        {
            Unsupported(what, statement.Location);
            return index;
        }

        // A message to or from the border is read from the part before its label
        if (DiagramDetector.IsBorderMessage(text))
        {
            Unsupported("messages from or to the diagram border ([->, ->], ?->) are not drawn", statement.Location);
            return index;
        }

        throw Error(
            $"'{Shorten(text)}' is not a sequence diagram statement",
            statement.Location,
            "write a message such as 'A -> B : text', a participant declaration, or a keyword statement");
    }

    /// <summary>
    ///     Decides whether a statement that starts with a participant keyword is a declaration (and not a message from a
    ///     participant whose name is that keyword).
    /// </summary>
    /// <param name="text">The statement.</param>
    /// <param name="word">The keyword.</param>
    /// <returns>True for a declaration.</returns>
    private static bool IsDeclaration(string text, string word)
    {
        var rest = text[word.Length..];
        return rest.Length > 0 && char.IsWhiteSpace(rest[0]) && !DiagramDetector.IsSequenceArrow(text);
    }

    /// <summary>
    ///     Decides whether a statement is a message between participants.
    /// </summary>
    /// <param name="text">The statement.</param>
    /// <param name="word">The first word in lower case.</param>
    /// <returns>True for a message.</returns>
    private static bool IsMessage(string text, string word)
    {
        // A keyword statement whose text holds an arrow-like run (a note's text, a group condition) is not a message
        if (word is "note" or "hnote" or "rnote" or "title" or "header" or "footer" or "caption" or "legend" or "alt" or "else" or
            "opt" or "loop" or "par" or "break" or "critical" or "group" or "box" or "ref" or "return" or "skinparam")
        {
            return false;
        }

        // A message to or from the diagram border is not drawn (the part before the label decides, so a label may
        // hold brackets and arrows)
        return DiagramDetector.IsSequenceArrow(text) && !DiagramDetector.IsBorderMessage(text);
    }

    /// <summary>
    ///     Builds the model: sorts the participants by their <c>order</c>, maps the events and boxes to the sorted
    ///     indexes, attaches the life changes to their messages, and applies the skin parameters.
    /// </summary>
    /// <param name="keywordLocation">The location the diagram type was detected at.</param>
    /// <returns>The model.</returns>
    private SequenceModel Build(PlantUmlLocation keywordLocation)
    {
        // A stable sort by the order values (a participant without one has order 0)
        var sorted = Enumerable.Range(0, _participants.Count)
            .OrderBy(i => _participants[i].Order ?? 0)
            .ThenBy(i => i)
            .ToList();
        var map = new int[sorted.Count];
        for (var position = 0; position < sorted.Count; position++)
        {
            map[sorted[position]] = position;
        }

        var participants = sorted.Select(i => _participants[i]).ToList();
        var events = new List<SequenceEvent>(_events.Count);
        for (var i = 0; i < _events.Count; i++)
        {
            events.Add(Remap(_events[i], map, _changes.GetValueOrDefault(i)));
        }

        var boxes = _boxes
            .Where(b => b.Members.Count > 0)
            .Select(b => new SequenceBox(b.Title, b.Members.Min(m => map[m]), b.Members.Max(m => map[m]), b.Location) { Fill = b.Fill })
            .ToList();

        var theme = SkinParams.Apply(SkinTheme.Default, _skin, _reports);
        if (_hideFootbox)
        {
            theme = theme with { Footbox = false };
        }

        // The skin parameters are reported when they are applied: put every report back in text order
        var reports = _reports.OrderBy(r => r.Location.Line).ThenBy(r => r.Location.Column).ToList();
        return new SequenceModel(participants, boxes, events, _page, theme, reports, keywordLocation) { Scale = _scale };
    }

    /// <summary>
    ///     Maps the participant indexes of an event to the sorted order and attaches its life changes.
    /// </summary>
    /// <param name="item">The event.</param>
    /// <param name="map">The sorted index of every declared index.</param>
    /// <param name="changes">The life changes of a message, or null.</param>
    /// <returns>The mapped event.</returns>
    private static SequenceEvent Remap(SequenceEvent item, int[] map, List<LifeChange>? changes)
    {
        LifeChange MapChange(LifeChange c) => c with { Participant = map[c.Participant] };
        return item switch
        {
            SequenceMessage m => m with
            {
                From = map[m.From],
                To = map[m.To],
                Changes = changes is null ? [] : changes.Select(MapChange).ToList(),
            },
            SequenceLifeEvent l => l with { Change = MapChange(l.Change) },
            SequenceNote n => n with { First = map[n.First], Last = map[n.Last] },
            _ => item,
        };
    }

    /// <summary>
    ///     Finds a participant by code, declaring it as a plain participant when it is new.
    /// </summary>
    /// <param name="code">The code.</param>
    /// <param name="location">The location of the use.</param>
    /// <returns>The declared index.</returns>
    private int Participant(string code, PlantUmlLocation location)
    {
        if (_byCode.TryGetValue(code, out var index))
        {
            return index;
        }

        return Declare(new SequenceParticipant(code, code, ParticipantKind.Participant, location));
    }

    /// <summary>
    ///     Declares a new participant.
    /// </summary>
    /// <param name="participant">The participant.</param>
    /// <returns>Its declared index.</returns>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the diagram has too many participants.</exception>
    private int Declare(SequenceParticipant participant)
    {
        if (_participants.Count >= _limits.MaxNodes)
        {
            throw new PlantUmlSyntaxException(
                PlantUmlDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"the diagram has more than {_limits.MaxNodes} participants (PlantUmlLimits.MaxNodes)"),
                participant.Location,
                "split the diagram, or raise PlantUmlLimits.MaxNodes when the source is trusted");
        }

        CheckLabel(participant.Display, participant.Location);
        _participants.Add(participant);
        var index = _participants.Count - 1;
        _byCode[participant.Code] = index;
        if (_openBox >= 0)
        {
            _boxes[_openBox].Members.Add(index);
        }

        return index;
    }

    /// <summary>
    ///     Adds an event.
    /// </summary>
    /// <param name="item">The event.</param>
    /// <returns>Its index.</returns>
    private int Add(SequenceEvent item)
    {
        _events.Add(item);
        if (item is not SequenceMessage and not SequenceNote and not SequenceLifeEvent)
        {
            // Life changes attach to the message right before them only
            _lastMessage = -1;
        }

        return _events.Count - 1;
    }

    /// <summary>
    ///     Checks a text against <see cref="PlantUmlLimits.MaxLabelLength"/>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="location">The location.</param>
    /// <exception cref="PlantUmlSyntaxException">Thrown when the text is too long.</exception>
    private void CheckLabel(string text, PlantUmlLocation location)
    {
        if (text.Length > _limits.MaxLabelLength)
        {
            throw new PlantUmlSyntaxException(
                PlantUmlDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"a text is {text.Length} characters long, more than the limit of {_limits.MaxLabelLength} (PlantUmlLimits.MaxLabelLength)"),
                location,
                "shorten the text, or raise PlantUmlLimits.MaxLabelLength when the source is trusted");
        }
    }

    /// <summary>
    ///     Reports a statement that is not drawn.
    /// </summary>
    /// <param name="what">What is not drawn.</param>
    /// <param name="location">The location.</param>
    private void Unsupported(string what, PlantUmlLocation location) =>
        _reports.Add(PlantUmlPendingReport.Unsupported(
            PlantUmlDiagnosticCodes.StatementUnsupported,
            what,
            location,
            "remove the statement, or render the diagram with PlantUML itself"));

    /// <summary>
    ///     Creates a located syntax error.
    /// </summary>
    /// <param name="description">The description.</param>
    /// <param name="location">The location.</param>
    /// <param name="hint">The hint.</param>
    /// <returns>The exception.</returns>
    private static PlantUmlSyntaxException Error(string description, PlantUmlLocation location, string hint) =>
        new(PlantUmlDiagnosticCodes.SyntaxError, description, location, hint);

    /// <summary>
    ///     Shortens a statement for a message.
    /// </summary>
    /// <param name="text">The statement.</param>
    /// <returns>At most 60 characters.</returns>
    private static string Shorten(string text) => text.Length <= 60 ? text : string.Concat(text.AsSpan(0, 57), "...");

    /// <summary>
    ///     Reads the arrow of a message: its body, its heads, and its decorations.
    /// </summary>
    private static class ArrowParser
    {
        /// <summary>
        ///     Reads an arrow.
        /// </summary>
        /// <param name="arrow">The arrow text (for example <c>-&gt;</c>, <c>--&gt;&gt;</c>, <c>&lt;-</c>, <c>-[#red]&gt;</c>).</param>
        /// <returns>The style and whether the arrow points to the left participant, or null when the text is not an arrow.</returns>
        public static (ArrowStyle Style, bool Reversed)? Read(string arrow)
        {
            var i = 0;
            var fromDecoration = i < arrow.Length && arrow[i] is 'o' or 'x' ? arrow[i++] : '\0';
            var leftHead = ReadHead(arrow, ref i, true);
            var dashes = 0;
            var dotted = false;
            Rgba32? color = null;
            while (i < arrow.Length && arrow[i] is '-' or '[')
            {
                if (arrow[i] == '-')
                {
                    dashes++;
                    i++;
                    continue;
                }

                var close = arrow.IndexOf(']', i);
                if (close < 0)
                {
                    return null;
                }

                ReadOptions(arrow[(i + 1)..close], ref color, ref dotted);
                i = close + 1;
            }

            var rightHead = ReadHead(arrow, ref i, false);
            var toDecoration = i < arrow.Length && arrow[i] is 'o' or 'x' ? arrow[i++] : '\0';
            if (dashes == 0 || i != arrow.Length)
            {
                return null;
            }

            // A cross replaces the head on its side
            leftHead = fromDecoration == 'x' ? ArrowHead.Cross : leftHead;
            rightHead = toDecoration == 'x' ? ArrowHead.Cross : rightHead;
            if (leftHead == ArrowHead.None && rightHead == ArrowHead.None)
            {
                return null;
            }

            dotted |= dashes >= 2;
            if (leftHead != ArrowHead.None && rightHead == ArrowHead.None)
            {
                var reversed = new ArrowStyle(dotted, ArrowHead.None, leftHead)
                {
                    FromCircle = toDecoration == 'o',
                    ToCircle = fromDecoration == 'o',
                    Color = color,
                };
                return (reversed, true);
            }

            var style = new ArrowStyle(dotted, leftHead, rightHead)
            {
                FromCircle = fromDecoration == 'o',
                ToCircle = toDecoration == 'o',
                Color = color,
            };
            return (style, false);
        }

        /// <summary>
        ///     Reads a head: one or two of <c>&lt;</c> (left) or <c>&gt;</c> (right) for a filled or open head, or one or
        ///     two slashes for a half head.
        /// </summary>
        /// <param name="arrow">The arrow text.</param>
        /// <param name="i">The index; advanced past the head.</param>
        /// <param name="left">Whether this is the left end.</param>
        /// <returns>The head, or <see cref="ArrowHead.None"/>.</returns>
        private static ArrowHead ReadHead(string arrow, ref int i, bool left)
        {
            if (i >= arrow.Length)
            {
                return ArrowHead.None;
            }

            var c = arrow[i];
            var full = left ? '<' : '>';
            if (c != full && c is not ('\\' or '/'))
            {
                return ArrowHead.None;
            }

            var count = 0;
            while (i < arrow.Length && arrow[i] == c && count < 2)
            {
                count++;
                i++;
            }

            if (c == full)
            {
                return count == 2 ? ArrowHead.Open : ArrowHead.Filled;
            }

            // The upper barb is '\' on the right end and '/' on the left end
            return (c == '\\') != left ? ArrowHead.HalfTop : ArrowHead.HalfBottom;
        }

        /// <summary>
        ///     Reads the options in the brackets of an arrow (<c>#red</c>, <c>dashed</c>, <c>dotted</c>, ...).
        /// </summary>
        /// <param name="options">The text between the brackets.</param>
        /// <param name="color">Receives the color of a color option.</param>
        /// <param name="dotted">Set by a dashed or dotted option.</param>
        private static void ReadOptions(string options, ref Rgba32? color, ref bool dotted)
        {
            foreach (var option in options.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (option.StartsWith('#') && SkinParams.TryParseColor(option, out var parsed))
                {
                    color = parsed;
                }
                else if (option.Equals("dashed", StringComparison.OrdinalIgnoreCase) || option.Equals("dotted", StringComparison.OrdinalIgnoreCase))
                {
                    dotted = true;
                }
            }
        }
    }
}
