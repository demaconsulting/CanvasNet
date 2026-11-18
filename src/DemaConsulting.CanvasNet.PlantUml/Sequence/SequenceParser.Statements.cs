// Portions derived from PlantUML (MIT); see third-party/README.md
using System.Globalization;
using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;
using DemaConsulting.CanvasNet.PlantUml.Syntax;

namespace DemaConsulting.CanvasNet.PlantUml.Sequence;

/// <content>
///     The readers of the individual statements.
/// </content>
internal sealed partial class SequenceParser
{
    /// <summary>The height of a <c>|||</c> space, in pixels.</summary>
    private const double DefaultSpace = 25;

    /// <summary>The largest diagram scale that is accepted.</summary>
    private const double MaxScale = 100;

    /// <summary>The event index of the last message of the diagram, or -1.</summary>
    private int _previousMessage = -1;

    /// <summary>Whether messages are numbered.</summary>
    private bool _numbering;

    /// <summary>The next message number.</summary>
    private long _nextNumber = 1;

    /// <summary>The step between message numbers.</summary>
    private long _numberStep = 1;

    /// <summary>The number format, or null for a bold number.</summary>
    private string? _numberFormat;

    /// <summary>
    ///     Reads a divider, a delay, or a space.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <returns>True when the statement is one of them.</returns>
    private bool TryReadSeparator(SourceStatement statement)
    {
        var text = statement.Text;
        if (text.StartsWith("==", StringComparison.Ordinal))
        {
            var inner = text.Trim('=').Trim();
            CheckLabel(inner, statement.Location);
            Add(new SequenceDivider(inner) { Location = statement.Location });
            return true;
        }

        if (text.StartsWith("...", StringComparison.Ordinal))
        {
            var inner = text.Trim('.').Trim();
            CheckLabel(inner, statement.Location);
            Add(new SequenceDelay(inner) { Location = statement.Location });
            return true;
        }

        if (!text.StartsWith("||", StringComparison.Ordinal))
        {
            return false;
        }

        // ||| is a default space; ||N|| is a space of N pixels
        var number = text.Trim('|').Trim();
        double height = DefaultSpace;
        if (number.Length > 0 &&
            !(double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out height) && height >= 0 && height <= 10_000))
        {
            throw Error($"'{Shorten(text)}' is not a space", statement.Location, "write '|||' or '||45||' with a height from 0 to 10000");
        }

        Add(new SequenceSpace(height) { Location = statement.Location });
        return true;
    }

    /// <summary>
    ///     Reads a participant declaration: <c>kind name [as alias] [#color] [order N]</c>.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="keywordLength">The length of the keyword.</param>
    private void ReadDeclaration(SourceStatement statement, ParticipantKind kind, int keywordLength)
    {
        var reader = new StatementReader(statement, keywordLength);
        var first = reader.ReadName() ?? throw Error("a participant declaration needs a name", statement.Location, "write for example 'participant Alice'");
        var (code, display) = (first.Text, first.Text);
        if (reader.TryReadWord("as"))
        {
            var second = reader.ReadName() ?? throw Error("'as' needs an alias", reader.Location, "write for example 'participant \"Long name\" as L'");
            // The quoted name is the display; with two plain names the second is the alias
            (code, display) = (second.Text, first.Text);
            if (!first.Quoted && second.Quoted)
            {
                (code, display) = (first.Text, second.Text);
            }
        }

        Rgba32? fill = null;
        int? order = null;
        while (!reader.AtEnd)
        {
            if (reader.TryReadColor(out var color, out var colorText))
            {
                fill = color ?? throw Error($"'{colorText}' is not a color", reader.Location, "write a color such as #LightBlue or #E0E0FF");
            }
            else if (reader.TryReadWord("order"))
            {
                order = reader.ReadInteger() ?? throw Error("'order' needs a whole number", reader.Location, "write for example 'order 10'");
            }
            else if (reader.TrySkipDelimited("<<", ">>"))
            {
                Unsupported("stereotypes of participants are not drawn", statement.Location);
            }
            else if (reader.TrySkipDelimited("[[", "]]"))
            {
                Unsupported("links of participants are not drawn", statement.Location);
            }
            else
            {
                throw Error($"'{Shorten(reader.Rest)}' is not expected in a participant declaration", reader.Location, "a declaration may hold 'as alias', '#color' and 'order N'");
            }
        }

        var participant = new SequenceParticipant(code, display, kind, statement.Location) { Fill = fill, Order = order };
        if (_byCode.TryGetValue(code, out var existing))
        {
            CheckLabel(display, statement.Location);
            _participants[existing] = participant;
            return;
        }

        Declare(participant);
    }

    /// <summary>
    ///     Reads a message: <c>A arrow B [shorthands] [: label]</c>.
    /// </summary>
    /// <param name="statement">The statement.</param>
    private void ReadMessage(SourceStatement statement)
    {
        var reader = new StatementReader(statement, 0);
        var left = reader.ReadName() ?? throw Error("a message needs a sender", statement.Location, "write for example 'Alice -> Bob : hello'");
        var arrowLocation = reader.Location;
        var arrowText = reader.ReadArrow();
        var arrow = ArrowParser.Read(arrowText) ??
            throw Error($"'{Shorten(arrowText)}' is not a message arrow", arrowLocation, "write an arrow such as ->, -->, ->>, ->x, -\\, ->o, <- or <->");
        var right = reader.ReadName() ?? throw Error("a message needs a receiver", reader.Location, "write for example 'Alice -> Bob : hello'");
        var shorthands = reader.ReadShorthands();
        Rgba32? activationFill = null;
        if (reader.TryReadColor(out var color, out var colorText))
        {
            activationFill = color ?? throw Error($"'{colorText}' is not a color", reader.Location, "write a color such as #Gold");
        }

        var label = reader.ReadLabel() ?? throw Error($"'{Shorten(reader.Rest)}' is not expected after the receiver", reader.Location, "write the label after a colon: 'A -> B : label'");
        CheckLabel(label, statement.Location);

        // The arrow points to the receiver: a head only on the left side means the right participant sends
        var leftIndex = Participant(left.Text, statement.Location);
        var rightIndex = Participant(right.Text, statement.Location);
        var (from, to, style) = arrow.Reversed
            ? (rightIndex, leftIndex, arrow.Style)
            : (leftIndex, rightIndex, arrow.Style);
        AddMessage(new SequenceMessage(from, to, label, style) { Location = statement.Location }, shorthands, activationFill);
    }

    /// <summary>
    ///     Adds a message with its number and its activation shorthands.
    /// </summary>
    /// <param name="message">The message (without number).</param>
    /// <param name="shorthands">The shorthands (<c>++</c>, <c>--</c>, <c>**</c>, <c>!!</c>) in order.</param>
    /// <param name="activationFill">The fill of an activation, or null.</param>
    private void AddMessage(SequenceMessage message, IReadOnlyList<string> shorthands, Rgba32? activationFill)
    {
        if (_events.OfType<SequenceMessage>().Count() >= _limits.MaxEdges)
        {
            throw new PlantUmlSyntaxException(
                PlantUmlDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"the diagram has more than {_limits.MaxEdges} messages (PlantUmlLimits.MaxEdges)"),
                message.Location,
                "split the diagram, or raise PlantUmlLimits.MaxEdges when the source is trusted");
        }

        if (_numbering)
        {
            message = message with { Number = FormatNumber(_nextNumber) };
            _nextNumber += _numberStep;
        }

        var index = Add(message);
        _lastMessage = index;
        _previousMessage = index;
        foreach (var shorthand in shorthands)
        {
            switch (shorthand)
            {
                case "++":
                    Attach(index, new LifeChange(message.To, LifeAction.Activate, activationFill));
                    _activating.Add(index);
                    break;
                case "--":
                    Attach(index, new LifeChange(message.From, LifeAction.Deactivate, null));
                    PopActivation();
                    break;
                case "!!":
                    Attach(index, new LifeChange(message.To, LifeAction.Destroy, null));
                    break;
                default:
                    Unsupported("participant creation (**) is not drawn; the participant is drawn at the top", message.Location);
                    break;
            }
        }
    }

    /// <summary>
    ///     Attaches a life change to a message.
    /// </summary>
    /// <param name="messageIndex">The event index of the message.</param>
    /// <param name="change">The change.</param>
    private void Attach(int messageIndex, LifeChange change)
    {
        if (!_changes.TryGetValue(messageIndex, out var list))
        {
            list = [];
            _changes[messageIndex] = list;
        }

        list.Add(change);
    }

    /// <summary>
    ///     Forgets the innermost activating message.
    /// </summary>
    private void PopActivation()
    {
        if (_activating.Count > 0)
        {
            _activating.RemoveAt(_activating.Count - 1);
        }
    }

    /// <summary>
    ///     Reads <c>activate X [#color]</c>, <c>deactivate X</c>, or <c>destroy X</c>.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="word">The keyword in lower case.</param>
    private void ReadLifeStatement(SourceStatement statement, string word)
    {
        var reader = new StatementReader(statement, word.Length);
        var name = reader.ReadName() ?? throw Error($"'{word}' needs a participant", statement.Location, $"write for example '{word} Alice'");
        Rgba32? fill = null;
        if (reader.TryReadColor(out var color, out var colorText))
        {
            fill = color ?? throw Error($"'{colorText}' is not a color", reader.Location, "write a color such as #Gold");
        }

        if (!reader.AtEnd)
        {
            throw Error($"'{Shorten(reader.Rest)}' is not expected after '{word} {name.Text}'", reader.Location, $"write '{word} {name.Text}'");
        }

        var participant = Participant(name.Text, statement.Location);
        var action = word switch
        {
            "activate" => LifeAction.Activate,
            "deactivate" => LifeAction.Deactivate,
            _ => LifeAction.Destroy,
        };
        var change = new LifeChange(participant, action, fill);

        // A change right after a message that involves the participant happens on that message's arrow
        if (_lastMessage >= 0 && _events[_lastMessage] is SequenceMessage last && (last.From == participant || last.To == participant))
        {
            Attach(_lastMessage, change);
            if (action == LifeAction.Activate)
            {
                _activating.Add(_lastMessage);
            }
        }
        else
        {
            Add(new SequenceLifeEvent(change) { Location = statement.Location });
        }

        if (action == LifeAction.Deactivate)
        {
            PopActivation();
        }
    }

    /// <summary>
    ///     Reads <c>return [label]</c>: a dotted reply to the sender of the innermost activating message, which also ends
    ///     that activation (or a reply to the last message when no activation is open).
    /// </summary>
    /// <param name="statement">The statement.</param>
    private void ReadReturn(SourceStatement statement)
    {
        var reader = new StatementReader(statement, "return".Length);
        Rgba32? color = null;
        if (reader.TryReadColor(out var parsed, out _))
        {
            color = parsed;
        }

        var label = reader.Rest;
        CheckLabel(label, statement.Location);
        var deactivate = _activating.Count > 0;
        var origin = deactivate ? _activating[^1] : _previousMessage;
        if (origin < 0 || _events[origin] is not SequenceMessage source)
        {
            throw Error("'return' has no message to return to", statement.Location, "write 'return' after a message that activates its receiver ('A -> B ++')");
        }

        var style = source.Arrow with { Dotted = true, Color = color ?? source.Arrow.Color };
        var (sender, receiver) = (source.To, source.From);
        var reply = new SequenceMessage(sender, receiver, label, style) { Location = statement.Location };
        AddMessage(reply, [], null);
        if (deactivate)
        {
            Attach(_lastMessage, new LifeChange(source.To, LifeAction.Deactivate, null));
            PopActivation();
        }
    }

    /// <summary>
    ///     Reads <c>autonumber [start] [step] ["format"]</c>, <c>autonumber stop</c>, and <c>autonumber resume</c>.
    /// </summary>
    /// <param name="statement">The statement.</param>
    private void ReadAutonumber(SourceStatement statement)
    {
        var reader = new StatementReader(statement, "autonumber".Length);
        if (reader.TryReadWord("stop"))
        {
            _numbering = false;
            return;
        }

        var resume = reader.TryReadWord("resume");
        if (!resume)
        {
            _nextNumber = reader.ReadInteger() ?? 1;
        }

        _numberStep = reader.ReadInteger() ?? (resume ? _numberStep : 1);
        if (reader.ReadName() is { Quoted: true } format)
        {
            _numberFormat = format.Text;
        }
        else if (!resume)
        {
            _numberFormat = null;
        }

        if (!reader.AtEnd)
        {
            Unsupported($"'autonumber {reader.Rest}' is not drawn; the messages are numbered 1, 2, 3, ...", statement.Location);
        }

        _numbering = true;
    }

    /// <summary>
    ///     Formats a message number: bold by default, or with the zeros of a format padded (<c>"&lt;b&gt;[000]"</c>).
    /// </summary>
    /// <param name="number">The number.</param>
    /// <returns>The display text.</returns>
    private string FormatNumber(long number)
    {
        var digits = number.ToString(CultureInfo.InvariantCulture);
        if (_numberFormat is null)
        {
            return "<b>" + digits;
        }

        // The first run of 0 and # outside a markup tag is the number
        var format = _numberFormat;
        var inTag = false;
        for (var i = 0; i < format.Length; i++)
        {
            var c = format[i];
            inTag = c == '<' || (inTag && c != '>');
            if (!inTag && c is '0' or '#')
            {
                var end = i;
                while (end < format.Length && format[end] is '0' or '#')
                {
                    end++;
                }

                var zeros = format[i..end].Count(ch => ch == '0');
                return string.Concat(format.AsSpan(0, i), digits.PadLeft(zeros, '0'), format.AsSpan(end));
            }
        }

        return format + digits;
    }

    /// <summary>
    ///     Reads a note: <c>note left|right [of X] [#color] : text</c>, <c>note over X[, Y] : text</c>,
    ///     <c>note across : text</c>, or the same without a colon and the text on the following lines up to
    ///     <c>end note</c>.
    /// </summary>
    /// <param name="index">The index of the statement.</param>
    /// <returns>The index of the last statement read.</returns>
    private int ReadNote(int index)
    {
        var statement = _statements[index];
        var word = DiagramDetector.FirstWord(statement.Text);
        var shape = word.ToLowerInvariant() switch
        {
            "hnote" => NoteShape.Hexagon,
            "rnote" => NoteShape.Box,
            _ => NoteShape.Note,
        };
        var reader = new StatementReader(statement, word.Length);
        var (position, first, last, onMessage) = ReadNotePlace(reader, statement);
        Rgba32? fill = null;
        if (reader.TryReadColor(out var color, out var colorText))
        {
            fill = color ?? throw Error($"'{colorText}' is not a color", reader.Location, "write a color such as #LightYellow");
        }

        string text;
        var label = reader.ReadLabel();
        if (label is not null && reader.ConsumedColon)
        {
            text = label;
        }
        else if (reader.AtEnd)
        {
            (text, index) = ReadBlock(index, "end note", "the note");
        }
        else
        {
            throw Error($"'{Shorten(reader.Rest)}' is not expected in a note", reader.Location, "write the text after a colon, or on the next lines up to 'end note'");
        }

        CheckLabel(text, statement.Location);
        Add(new SequenceNote(position, first, last, text, shape) { Fill = fill, OnMessage = onMessage, Location = statement.Location });
        return index;
    }

    /// <summary>
    ///     Reads where a note is drawn.
    /// </summary>
    /// <param name="reader">The reader after the keyword.</param>
    /// <param name="statement">The statement.</param>
    /// <returns>The position, the participants, and whether the note is on the previous message's row.</returns>
    private (NotePosition Position, int First, int Last, bool OnMessage) ReadNotePlace(StatementReader reader, SourceStatement statement)
    {
        if (reader.TryReadWord("over"))
        {
            var a = reader.ReadName() ?? throw Error("'note over' needs a participant", reader.Location, "write for example 'note over Alice : text'");
            var first = Participant(a.Text, statement.Location);
            var last = first;
            if (reader.TryReadChar(','))
            {
                var b = reader.ReadName() ?? throw Error("'note over A,' needs a second participant", reader.Location, "write for example 'note over Alice, Bob : text'");
                last = Participant(b.Text, statement.Location);
            }

            return (NotePosition.Over, first, last, false);
        }

        if (reader.TryReadWord("across"))
        {
            if (_participants.Count == 0)
            {
                throw Error("'note across' needs participants", statement.Location, "declare the participants first");
            }

            return (NotePosition.Over, 0, _participants.Count - 1, false);
        }

        NotePosition position;
        if (reader.TryReadWord("left"))
        {
            position = NotePosition.Left;
        }
        else if (reader.TryReadWord("right"))
        {
            position = NotePosition.Right;
        }
        else
        {
            throw Error("a note needs a position: left, right, over, or across", reader.Location, "write for example 'note left of Alice : text'");
        }

        if (reader.TryReadWord("of"))
        {
            var name = reader.ReadName() ?? throw Error("'of' needs a participant", reader.Location, "write for example 'note left of Alice : text'");
            var participant = Participant(name.Text, statement.Location);
            return (position, participant, participant, false);
        }

        // Without a participant the note belongs to the message right before it
        if (_previousMessage < 0 || _events[_previousMessage] is not SequenceMessage message)
        {
            throw Error("a note without a participant must follow a message", statement.Location, "write 'note left of Alice : text'");
        }

        var onRow = _events.Count - 1 == _previousMessage;
        if (onRow)
        {
            return (position, message.From, message.To, true);
        }

        // A later note sits beside the participant on that side of the message
        var side = position == NotePosition.Left ? Math.Min(message.From, message.To) : Math.Max(message.From, message.To);
        return (position, side, side, false);
    }

    /// <summary>
    ///     Reads the lines of a text block up to its closing keyword.
    /// </summary>
    /// <param name="index">The index of the opening statement.</param>
    /// <param name="closer">The closing keyword.</param>
    /// <param name="what">What the block is, for the error.</param>
    /// <returns>The text (lines joined with line feeds) and the index of the closing statement.</returns>
    private (string Text, int Index) ReadBlock(int index, string closer, string what)
    {
        var lines = new List<string>();
        var i = index + 1;
        while (i < _statements.Count && !DiagramDetector.IsCloser(_statements[i].Text, closer))
        {
            lines.Add(_statements[i].Text);
            i++;
        }

        if (i >= _statements.Count)
        {
            throw Error($"{what} is not closed", _statements[index].Location, $"close it with '{closer}'");
        }

        return (string.Join('\n', lines), i);
    }

    /// <summary>
    ///     Reads a page text: <c>title</c>, <c>header</c>, <c>footer</c>, <c>caption</c> (on one line or as a block),
    ///     <c>left|center|right header|footer</c>, or <c>legend [left|right|center]</c> ... <c>endlegend</c>.
    /// </summary>
    /// <param name="index">The index of the statement.</param>
    /// <param name="word">The first word in lower case.</param>
    /// <returns>The index of the last statement read.</returns>
    private int ReadPageText(int index, string word)
    {
        var statement = _statements[index];
        var reader = new StatementReader(statement, word.Length);
        TextAlign? align = null;
        if (word is "left" or "right" or "center")
        {
            align = ParseAlign(word);
            if (reader.TryReadWord("header"))
            {
                word = "header";
            }
            else if (reader.TryReadWord("footer"))
            {
                word = "footer";
            }
            else
            {
                throw Error($"'{word}' must be followed by 'header' or 'footer'", reader.Location, $"write for example '{word} header text'");
            }
        }

        string text;
        if (word == "legend")
        {
            while (reader.ReadWord() is { } alignWord)
            {
                align = alignWord.ToLowerInvariant() is "left" or "right" or "center" ? ParseAlign(alignWord) : align;
            }

            (text, index) = ReadBlock(index, "endlegend", "the legend");
        }
        else if (reader.AtEnd)
        {
            (text, index) = ReadBlock(index, "end" + word, $"the {word}");
        }
        else
        {
            reader.TryReadChar(':');
            text = reader.Rest;
        }

        CheckLabel(text, statement.Location);
        SetPageText(word, text, align, statement.Location);
        return index;
    }

    /// <summary>
    ///     Stores a page text.
    /// </summary>
    /// <param name="word">The kind of page text.</param>
    /// <param name="text">The text.</param>
    /// <param name="align">The written alignment, or null for the default.</param>
    /// <param name="location">The location.</param>
    private void SetPageText(string word, string text, TextAlign? align, PlantUmlLocation location)
    {
        _page = word switch
        {
            "title" => _page with { Title = new PageText(text, TextAlign.Center, location) },
            "header" => _page with { Header = new PageText(text, align ?? TextAlign.Right, location) },
            "footer" => _page with { Footer = new PageText(text, align ?? TextAlign.Center, location) },
            "caption" => _page with { Caption = new PageText(text, TextAlign.Center, location) },
            _ => _page with { Legend = new PageText(text, align ?? TextAlign.Center, location) },
        };
    }

    /// <summary>
    ///     Reads an alignment word.
    /// </summary>
    /// <param name="word">The word.</param>
    /// <returns>The alignment.</returns>
    private static TextAlign ParseAlign(string word) => word.ToLowerInvariant() switch
    {
        "left" => TextAlign.Left,
        "right" => TextAlign.Right,
        _ => TextAlign.Center,
    };

    /// <summary>
    ///     Reads a group start: <c>alt|opt|loop|par|break|critical [#color] [condition]</c> or
    ///     <c>group [title] [[condition]]</c>.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="word">The keyword in lower case.</param>
    private void ReadGroup(SourceStatement statement, string word)
    {
        if (_groups.Count >= _limits.MaxNestingDepth)
        {
            throw new PlantUmlSyntaxException(
                PlantUmlDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"the groups nest more than {_limits.MaxNestingDepth} deep (PlantUmlLimits.MaxNestingDepth)"),
                statement.Location,
                "nest fewer groups, or raise PlantUmlLimits.MaxNestingDepth when the source is trusted");
        }

        var (fill, label) = ReadGroupText(statement, word.Length);
        var keyword = word;
        if (word == "group")
        {
            // group Title [condition]: the title is drawn in the tab (PlantUML keeps the space before the bracket,
            // which widens the tab)
            var open = label?.IndexOf('[', StringComparison.Ordinal) ?? -1;
            var title = open >= 0 ? label![..open] : label;
            var condition = open >= 0 ? label![(open + 1)..].TrimEnd().TrimEnd(']').Trim() : null;
            keyword = string.IsNullOrWhiteSpace(title) ? "group" : title;
            label = string.IsNullOrEmpty(condition) ? null : condition;
        }

        Add(new SequenceGroupStart(keyword, label) { Fill = fill, Location = statement.Location });
        _groups.Add((word, statement.Location));
    }

    /// <summary>
    ///     Reads the colors and the text after a group keyword.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="keywordLength">The length of the keyword.</param>
    /// <returns>The frame fill (or null) and the text (or null when there is none).</returns>
    private (Rgba32? Fill, string? Label) ReadGroupText(SourceStatement statement, int keywordLength)
    {
        var text = statement.Text;

        // alt#Gold: the color of the header glued to the keyword is not drawn
        var start = keywordLength;
        if (start < text.Length && text[start] == '#')
        {
            while (start < text.Length && !char.IsWhiteSpace(text[start]))
            {
                start++;
            }
        }

        var reader = new StatementReader(statement, start);
        Rgba32? fill = null;
        if (reader.TryReadColor(out var color, out _))
        {
            fill = color;
        }

        var label = reader.Rest;
        CheckLabel(label, statement.Location);
        return (fill, label.Length == 0 ? null : label);
    }

    /// <summary>
    ///     Reads <c>else [condition]</c>.
    /// </summary>
    /// <param name="statement">The statement.</param>
    private void ReadElse(SourceStatement statement)
    {
        if (_groups.Count == 0)
        {
            throw Error("'else' is outside a group", statement.Location, "write 'else' between 'alt' and 'end'");
        }

        var (_, label) = ReadGroupText(statement, "else".Length);
        Add(new SequenceGroupElse(label) { Location = statement.Location });
    }

    /// <summary>
    ///     Reads <c>end</c> (closing a group) or <c>end box</c>.
    /// </summary>
    /// <param name="statement">The statement.</param>
    private void ReadEnd(SourceStatement statement)
    {
        var rest = statement.Text[3..].Trim().ToLowerInvariant();
        if (rest == "box")
        {
            CloseBox(statement);
            return;
        }

        if (rest.Length > 0 && !GroupKeywords.Contains(rest))
        {
            throw Error($"'{Shorten(statement.Text)}' does not close anything", statement.Location, "write 'end' to close a group or 'end box' to close a box");
        }

        if (_groups.Count == 0)
        {
            throw Error("'end' has no group to close", statement.Location, "remove the 'end', or open a group with 'alt', 'loop', ...");
        }

        _groups.RemoveAt(_groups.Count - 1);
        Add(new SequenceGroupEnd { Location = statement.Location });
    }

    /// <summary>
    ///     Reads <c>box ["title"] [#color]</c>.
    /// </summary>
    /// <param name="statement">The statement.</param>
    private void ReadBox(SourceStatement statement)
    {
        if (_openBox >= 0)
        {
            throw Error("boxes cannot be nested", statement.Location, "close the open box with 'end box' first");
        }

        var reader = new StatementReader(statement, "box".Length);
        string? title = null;
        Rgba32? fill = null;
        while (!reader.AtEnd)
        {
            if (reader.TryReadColor(out var color, out var colorText))
            {
                fill = color ?? throw Error($"'{colorText}' is not a color", reader.Location, "write a color such as #LightBlue");
            }
            else if (reader.Rest.StartsWith('"') && reader.ReadName() is { } quoted)
            {
                title = quoted.Text;
            }
            else
            {
                // An unquoted title runs up to a trailing color
                var rest = reader.Rest;
                var hash = rest.LastIndexOf(" #", StringComparison.Ordinal);
                if (hash > 0 && SkinParams.TryParseColor(rest[(hash + 1)..], out var trailing))
                {
                    fill = trailing;
                    rest = rest[..hash].TrimEnd();
                }

                title = rest;
                break;
            }
        }

        if (title is not null)
        {
            CheckLabel(title, statement.Location);
        }

        if (_boxes.Count >= _limits.MaxNodes)
        {
            throw new PlantUmlSyntaxException(
                PlantUmlDiagnosticCodes.LimitExceeded,
                string.Create(CultureInfo.InvariantCulture, $"the diagram has more than {_limits.MaxNodes} boxes (PlantUmlLimits.MaxNodes)"),
                statement.Location,
                "split the diagram, or raise PlantUmlLimits.MaxNodes when the source is trusted");
        }

        _boxes.Add((title, fill, [], statement.Location));
        _openBox = _boxes.Count - 1;
    }

    /// <summary>
    ///     Closes the open box.
    /// </summary>
    /// <param name="statement">The <c>end box</c> statement.</param>
    private void CloseBox(SourceStatement statement)
    {
        if (_openBox < 0)
        {
            throw Error("'end box' has no box to close", statement.Location, "open a box with 'box \"title\"' first");
        }

        _openBox = -1;
    }

    /// <summary>
    ///     Reads <c>hide footbox</c> and <c>show footbox</c>; other <c>hide</c> and <c>show</c> statements are not
    ///     drawn.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="word">The keyword.</param>
    private void ReadHideShow(SourceStatement statement, string word)
    {
        var rest = statement.Text[word.Length..].Trim();
        if (rest.Equals("footbox", StringComparison.OrdinalIgnoreCase))
        {
            _hideFootbox = word == "hide";
            return;
        }

        Unsupported($"'{Shorten(statement.Text)}' is not drawn", statement.Location);
    }

    /// <summary>
    ///     Reads <c>scale N</c> or <c>scale N/M</c>; other forms are not drawn.
    /// </summary>
    /// <param name="statement">The statement.</param>
    private void ReadScale(SourceStatement statement)
    {
        var rest = statement.Text["scale".Length..].Trim();
        var slash = rest.IndexOf('/', StringComparison.Ordinal);
        double value;
        bool ok;
        if (slash > 0)
        {
            var numeratorOk = double.TryParse(rest[..slash], NumberStyles.Float, CultureInfo.InvariantCulture, out var numerator);
            var denominatorOk = double.TryParse(rest[(slash + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out var denominator);
            value = numerator / denominator;
            ok = numeratorOk && denominatorOk && double.IsFinite(value);
        }
        else
        {
            ok = double.TryParse(rest, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        if (ok && value > 0 && value <= MaxScale)
        {
            _scale = value;
            return;
        }

        Unsupported($"'{Shorten(statement.Text)}' is not drawn; only 'scale N' from 0 to {MaxScale.ToString(CultureInfo.InvariantCulture)} is", statement.Location);
    }


    /// <summary>
    ///     Skips a <c>ref over</c> statement (and its block) and reports it.
    /// </summary>
    /// <param name="index">The index of the statement.</param>
    /// <returns>The index of the last statement read.</returns>
    private int SkipRef(int index)
    {
        var statement = _statements[index];
        Unsupported("references (ref over) are not drawn", statement.Location);
        if (DiagramDetector.BlockCloser(statement.Text) is { } closer)
        {
            (_, index) = ReadBlock(index, closer, "the reference");
        }

        return index;
    }

    /// <summary>
    ///     A name read from a statement: its text and whether it was quoted.
    /// </summary>
    /// <param name="Text">The text, without quotes.</param>
    /// <param name="Quoted">Whether it was quoted.</param>
    private sealed record Name(string Text, bool Quoted);

    /// <summary>
    ///     Reads the parts of one statement from left to right.
    /// </summary>
    /// <param name="statement">The statement.</param>
    /// <param name="start">The index to start at.</param>
    private sealed class StatementReader(SourceStatement statement, int start)
    {
        /// <summary>The text.</summary>
        private readonly string _text = statement.Text;

        /// <summary>The current index.</summary>
        private int _pos = start;

        /// <summary>
        ///     Gets a value indicating whether only white space is left.
        /// </summary>
        public bool AtEnd
        {
            get
            {
                SkipSpace();
                return _pos >= _text.Length;
            }
        }

        /// <summary>
        ///     Gets the rest of the statement, trimmed.
        /// </summary>
        public string Rest => _pos >= _text.Length ? string.Empty : _text[_pos..].Trim();

        /// <summary>
        ///     Gets the location of the current index.
        /// </summary>
        public PlantUmlLocation Location => statement.Locate(_pos);

        /// <summary>
        ///     Gets a value indicating whether <see cref="ReadLabel"/> consumed a colon.
        /// </summary>
        public bool ConsumedColon { get; private set; }

        /// <summary>
        ///     Reads a name: a quoted text, or a run of letters, digits, and <c>_ . @ $</c>.
        /// </summary>
        /// <returns>The name, or null when there is none.</returns>
        public Name? ReadName()
        {
            SkipSpace();
            if (_pos >= _text.Length)
            {
                return null;
            }

            if (_text[_pos] == '"')
            {
                var close = _text.IndexOf('"', _pos + 1);
                if (close < 0)
                {
                    return null;
                }

                var quoted = _text[(_pos + 1)..close];
                _pos = close + 1;
                return new Name(quoted, true);
            }

            var begin = _pos;
            while (_pos < _text.Length && IsNameChar(_text[_pos]))
            {
                _pos++;
            }

            return _pos > begin ? new Name(_text[begin.._pos], false) : null;
        }

        /// <summary>
        ///     Reads a word of letters.
        /// </summary>
        /// <returns>The word, or null.</returns>
        public string? ReadWord()
        {
            SkipSpace();
            var begin = _pos;
            while (_pos < _text.Length && char.IsAsciiLetter(_text[_pos]))
            {
                _pos++;
            }

            return _pos > begin ? _text[begin.._pos] : null;
        }

        /// <summary>
        ///     Reads a keyword when it is next (followed by a non-letter).
        /// </summary>
        /// <param name="word">The keyword.</param>
        /// <returns>True when it was read.</returns>
        public bool TryReadWord(string word)
        {
            SkipSpace();
            if (string.Compare(_text, _pos, word, 0, word.Length, StringComparison.OrdinalIgnoreCase) != 0)
            {
                return false;
            }

            var end = _pos + word.Length;
            if (end < _text.Length && char.IsAsciiLetterOrDigit(_text[end]))
            {
                return false;
            }

            _pos = end;
            return true;
        }

        /// <summary>
        ///     Reads a character when it is next.
        /// </summary>
        /// <param name="c">The character.</param>
        /// <returns>True when it was read.</returns>
        public bool TryReadChar(char c)
        {
            SkipSpace();
            if (_pos < _text.Length && _text[_pos] == c)
            {
                _pos++;
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Reads a whole number.
        /// </summary>
        /// <returns>The number, or null when there is none.</returns>
        public int? ReadInteger()
        {
            SkipSpace();
            var begin = _pos;
            if (_pos < _text.Length && _text[_pos] == '-')
            {
                _pos++;
            }

            while (_pos < _text.Length && char.IsAsciiDigit(_text[_pos]))
            {
                _pos++;
            }

            if (int.TryParse(_text.AsSpan(begin, _pos - begin), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }

            _pos = begin;
            return null;
        }

        /// <summary>
        ///     Reads a <c>#color</c> token when one is next.
        /// </summary>
        /// <param name="color">Receives the color, or null when the token is not a color.</param>
        /// <param name="text">Receives the token.</param>
        /// <returns>True when a token starting with <c>#</c> was read.</returns>
        public bool TryReadColor(out Rgba32? color, out string text)
        {
            color = null;
            text = string.Empty;
            SkipSpace();
            if (_pos >= _text.Length || _text[_pos] != '#')
            {
                return false;
            }

            var begin = _pos;
            while (_pos < _text.Length && !char.IsWhiteSpace(_text[_pos]) && _text[_pos] != ':' && _text[_pos] != ';')
            {
                _pos++;
            }

            // A line style after the color (#red;line:blue) is not drawn
            while (_pos < _text.Length && _text[_pos] == ';')
            {
                while (_pos < _text.Length && !char.IsWhiteSpace(_text[_pos]) && _text[_pos] != ':')
                {
                    _pos++;
                }
            }

            text = _text[begin.._pos];
            var colorText = text.Split(';')[0];
            color = SkinParams.TryParseColor(colorText, out var parsed) ? parsed : null;
            return true;
        }

        /// <summary>
        ///     Skips a delimited part (<c>&lt;&lt;...&gt;&gt;</c>) when it is next.
        /// </summary>
        /// <param name="open">The opening delimiter.</param>
        /// <param name="close">The closing delimiter.</param>
        /// <returns>True when it was skipped.</returns>
        public bool TrySkipDelimited(string open, string close)
        {
            SkipSpace();
            if (string.CompareOrdinal(_text, _pos, open, 0, open.Length) != 0)
            {
                return false;
            }

            var end = _text.IndexOf(close, _pos + open.Length, StringComparison.Ordinal);
            _pos = end < 0 ? _text.Length : end + close.Length;
            return true;
        }

        /// <summary>
        ///     Reads the arrow of a message: the run of arrow characters after the sender.
        /// </summary>
        /// <returns>The arrow text, possibly empty.</returns>
        public string ReadArrow()
        {
            SkipSpace();
            var begin = _pos;
            var builder = new StringBuilder();
            while (_pos < _text.Length)
            {
                var c = _text[_pos];
                if (c == '[')
                {
                    var close = _text.IndexOf(']', _pos);
                    var end = close < 0 ? _text.Length : close + 1;
                    builder.Append(_text, _pos, end - _pos);
                    _pos = end;
                    continue;
                }

                if (c is 'o' or 'x' && !IsArrowDecoration(begin))
                {
                    break;
                }

                if (c is not ('-' or '<' or '>' or '\\' or '/' or 'o' or 'x'))
                {
                    break;
                }

                builder.Append(c);
                _pos++;
            }

            return builder.ToString();
        }

        /// <summary>
        ///     Reads the activation shorthands after the receiver (<c>++</c>, <c>--</c>, <c>**</c>, <c>!!</c>).
        /// </summary>
        /// <returns>The shorthands in order.</returns>
        public List<string> ReadShorthands()
        {
            var shorthands = new List<string>();
            SkipSpace();
            while (_pos + 1 < _text.Length && _text[_pos] == _text[_pos + 1] && _text[_pos] is '+' or '-' or '*' or '!')
            {
                shorthands.Add(_text.Substring(_pos, 2));
                _pos += 2;
                SkipSpace();
            }

            return shorthands;
        }

        /// <summary>
        ///     Reads a label: the rest after a colon, or nothing when the statement ends.
        /// </summary>
        /// <returns>The label (empty when there is none), or null when something else is next.</returns>
        public string? ReadLabel()
        {
            SkipSpace();
            if (_pos >= _text.Length)
            {
                return string.Empty;
            }

            if (_text[_pos] != ':')
            {
                return null;
            }

            ConsumedColon = true;
            var label = _text[(_pos + 1)..].Trim();
            _pos = _text.Length;
            return label;
        }

        /// <summary>
        ///     Decides whether a character can be part of a name.
        /// </summary>
        /// <param name="c">The character.</param>
        /// <returns>True when it can.</returns>
        private static bool IsNameChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '.' or '@' or '$' || c > 0x7F && !char.IsWhiteSpace(c);

        /// <summary>
        ///     Decides whether an <c>o</c> or <c>x</c> at the current index decorates the arrow: at its start before a
        ///     head or a dash, or at its end before the receiver's white space.
        /// </summary>
        /// <param name="begin">The index where the arrow starts.</param>
        /// <returns>True when it is a decoration.</returns>
        private bool IsArrowDecoration(int begin)
        {
            var next = _pos + 1 < _text.Length ? _text[_pos + 1] : ' ';
            if (_pos == begin)
            {
                return next is '<' or '-' or '\\' or '/';
            }

            return char.IsWhiteSpace(next) || next is ':' || _pos + 1 >= _text.Length;
        }

        /// <summary>
        ///     Skips white space.
        /// </summary>
        private void SkipSpace()
        {
            while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos]))
            {
                _pos++;
            }
        }
    }
}
