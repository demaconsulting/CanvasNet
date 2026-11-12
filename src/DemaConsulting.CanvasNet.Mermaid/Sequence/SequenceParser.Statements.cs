// Portions derived from mermaid (MIT); see third-party/README.md
using System.Globalization;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Sequence;

/// <content>
///     The statements of the grammar: each method parses one statement and lists, in the order of Mermaid's statement
///     arrays, the statements it hands to the database.
/// </content>
internal sealed partial class SequenceParser
{
    /// <summary>
    ///     Parses one statement (<c>statement</c>), dispatched on its first token.
    /// </summary>
    private void ParseStatement()
    {
        switch (Current.Kind)
        {
            case SequenceTokens.Participant or SequenceTokens.ParticipantActor or SequenceTokens.Destroy:
                ParseParticipantStatement(null);
                break;
            case SequenceTokens.Create:
                var create = Advance();
                if (!Is(ParticipantKeywords))
                {
                    throw Fail(ParticipantKeywords);
                }

                ParseParticipantStatement(Locate(create));
                break;
            case SequenceTokens.Box:
                ParseBox();
                break;
            case SequenceTokens.Autonumber:
                ParseAutonumber();
                break;
            case SequenceTokens.Activate or SequenceTokens.Deactivate:
                ParseActivation();
                break;
            case SequenceTokens.Note:
                ParseNote();
                break;
            case SequenceTokens.Links or SequenceTokens.Link or SequenceTokens.Properties or SequenceTokens.Details:
                ParseActorData();
                break;
            case SequenceTokens.Title or SequenceTokens.LegacyTitle:
                // "title " or "title: " and its white-space character are cut, the rest is kept as written
                var title = Advance();
                _model.SetDiagramTitle(TextOf(title)[(title.Kind == SequenceTokens.Title ? 6 : 7)..]);
                break;
            case SequenceTokens.AccTitle:
                Advance();
                _model.SetAccessibleTitle(Preprocessor.TrimJs(TextOf(Expect(SequenceTokens.AccTitleValue))));
                break;
            case SequenceTokens.AccDescr:
                Advance();
                _model.SetAccessibleDescription(Preprocessor.TrimJs(TextOf(Expect(SequenceTokens.AccDescrValue))));
                break;
            case SequenceTokens.AccDescrMultilineValue:
                _model.SetAccessibleDescription(Preprocessor.TrimJs(TextOf(Advance())));
                break;
            case SequenceTokens.Actor:
                ParseSignal();
                break;
            default:
                ParseBlock();
                break;
        }
    }

    /// <summary>
    ///     Parses a participant statement (<c>participant_statement</c>): <c>participant</c> or <c>actor</c>, a name, optional
    ///     <c>@{ ... }</c> metadata and an optional alias; or <c>destroy</c> and a name.
    /// </summary>
    /// <param name="createLocation">The location of a <c>create</c> before it, or null.</param>
    private void ParseParticipantStatement(MermaidLocation? createLocation)
    {
        var keyword = Advance();
        var location = createLocation ?? Locate(keyword);
        if (keyword.Kind == SequenceTokens.Destroy)
        {
            // A created destroy statement is a creation without a keyword type, as Mermaid's grammar retypes it
            var destroyed = ExpectActor();
            Expect(SequenceTokens.Newline);
            _statements.Add(createLocation is null
                ? new SequenceDestroyStatement(destroyed, location)
                : new SequenceParticipantStatement(destroyed, null, null, null, true, location));
            return;
        }

        // The name, then (in the state shared with no other statement) metadata, an alias or the end
        var name = TextOf(Expect(SequenceTokens.Actor));
        SequenceMetadataText? config = null;
        if (Is(SequenceTokens.ConfigStart))
        {
            Advance();
            config = Metadata(Expect(SequenceTokens.ConfigContent));
            Expect(SequenceTokens.ConfigEnd);
            CheckLookahead(AliasOrNewline);
        }
        else
        {
            CheckLookahead(DeclaredNameFollowers);
        }

        SequenceText? description = null;
        if (Is(SequenceTokens.As))
        {
            Advance();
            description = SequenceText.Parse(TextOf(Expect(SequenceTokens.RestOfLine)));
        }

        Expect(SequenceTokens.Newline);
        var draw = keyword.Kind == SequenceTokens.ParticipantActor ? SequenceParticipantTypes.Actor : SequenceParticipantTypes.Participant;
        _statements.Add(new SequenceParticipantStatement(name, draw, description, config, createLocation is not null, location));
    }

    /// <summary>
    ///     Makes the metadata of a <c>CONFIG_CONTENT</c> token: its text trimmed, with positions mapped to the original.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The metadata.</returns>
    private SequenceMetadataText Metadata(ScanToken token)
    {
        var leading = 0;
        while (leading < token.Text.Length && ScanPatterns.IsJsWhitespace(token.Text[leading]))
        {
            leading++;
        }

        var text = Preprocessor.TrimJs(token.Text);
        var scanner = _scanner;
        return new SequenceMetadataText(text, position => scanner.Locate(token.Start + leading + Math.Clamp(position, 0, text.Length)));
    }

    /// <summary>
    ///     Parses a box: <c>box</c>, its color and title, participant statements and <c>end</c>.
    /// </summary>
    private void ParseBox()
    {
        var keyword = Advance();
        Enter(Locate(keyword));
        var data = SequenceModel.ParseBoxData(TextOf(Expect(SequenceTokens.RestOfLine)));
        _statements.Add(new SequenceBoxStartStatement(data, Locate(keyword)));
        while (!Is(SequenceTokens.End))
        {
            if (Is(SequenceTokens.Newline))
            {
                Advance();
            }
            else if (Is(ParticipantKeywords))
            {
                ParseParticipantStatement(null);
                CheckLookahead(LineFollowers, endAllowed: true);
            }
            else
            {
                throw Fail(BoxLineTokens);
            }
        }

        var end = Advance();
        _statements.Add(new SequenceBoxEndStatement(Locate(end)));
        Leave();
    }

    /// <summary>
    ///     Parses an <c>autonumber</c> statement: alone, with a start, with a start and a step, or <c>off</c>.
    /// </summary>
    private void ParseAutonumber()
    {
        var location = Locate(Advance());
        if (Is(SequenceTokens.Num))
        {
            var start = Number(Advance());
            double step = 1;
            if (Is(SequenceTokens.Num))
            {
                step = Number(Advance());
            }
            else if (!Is(SequenceTokens.Newline))
            {
                throw Fail([SequenceTokens.Newline, SequenceTokens.Num]);
            }

            Expect(SequenceTokens.Newline);
            _statements.Add(new SequenceAutonumberStatement(start, step, true, location));
        }
        else if (Is(SequenceTokens.Off))
        {
            Advance();
            Expect(SequenceTokens.Newline);
            _statements.Add(new SequenceAutonumberStatement(null, null, false, location));
        }
        else if (Is(SequenceTokens.Newline))
        {
            Advance();
            _statements.Add(new SequenceAutonumberStatement(null, null, true, location));
        }
        else
        {
            throw Fail([SequenceTokens.Newline, SequenceTokens.Num, SequenceTokens.Off]);
        }
    }

    /// <summary>
    ///     Parses an <c>activate</c> or <c>deactivate</c> statement.
    /// </summary>
    private void ParseActivation()
    {
        var keyword = Advance();
        var actor = ExpectActor();
        Expect(SequenceTokens.Newline);
        var type = keyword.Kind == SequenceTokens.Activate ? SequenceLineType.ActiveStart : SequenceLineType.ActiveEnd;
        _statements.Add(new SequenceActorSignalStatement(actor, type, Locate(keyword)));
    }

    /// <summary>
    ///     Parses a note: <c>note left of</c>, <c>note right of</c> or <c>note over</c> one or two participants, then its
    ///     text.
    /// </summary>
    private void ParseNote()
    {
        var location = Locate(Advance());
        int placement;
        string from;
        var to = (string?)null;
        if (Is(SequenceTokens.LeftOf, SequenceTokens.RightOf))
        {
            placement = Advance().Kind == SequenceTokens.LeftOf ? SequencePlacement.LeftOf : SequencePlacement.RightOf;
            from = ExpectActor();
        }
        else if (Is(SequenceTokens.Over))
        {
            Advance();
            placement = SequencePlacement.Over;
            from = ExpectActor();

            // actor_pair: a comma and a second participant, or the text
            if (Is(SequenceTokens.Comma))
            {
                Advance();
                to = ExpectActor();
            }
            else if (!Is(SequenceTokens.Txt))
            {
                throw Fail([SequenceTokens.Comma, SequenceTokens.Txt]);
            }
        }
        else
        {
            throw Fail([SequenceTokens.Over, SequenceTokens.LeftOf, SequenceTokens.RightOf]);
        }

        var text = ExpectText();
        Expect(SequenceTokens.Newline);
        Mention(from, location);
        if (to is not null)
        {
            Mention(to, location);
        }

        _statements.Add(new SequenceNoteStatement(from, to ?? from, placement, text, location));
    }

    /// <summary>
    ///     Parses a <c>links</c>, <c>link</c>, <c>properties</c> or <c>details</c> statement: a participant and a text.
    /// </summary>
    private void ParseActorData()
    {
        var keyword = Advance();
        var location = Locate(keyword);
        var actor = ExpectActor();
        var text = ExpectText();
        Expect(SequenceTokens.Newline);
        var kind = keyword.Kind switch
        {
            SequenceTokens.Links => SequenceActorDataKind.Links,
            SequenceTokens.Link => SequenceActorDataKind.Link,
            SequenceTokens.Properties => SequenceActorDataKind.Properties,
            _ => SequenceActorDataKind.Details,
        };
        Mention(actor, location);
        _statements.Add(new SequenceActorDataStatement(kind, actor, text, location));
    }

    /// <summary>
    ///     Parses a block: its keyword and title, its lines, its sections (<c>else</c>, <c>and</c> or <c>option</c>, each
    ///     with a title and lines) and <c>end</c>.
    /// </summary>
    private void ParseBlock()
    {
        var keyword = Current;
        if (!Blocks.TryGetValue(keyword.Kind, out var block))
        {
            // Unreachable: every line start is dispatched to a statement
            throw Fail(LineStarts);
        }

        Advance();
        var location = Locate(keyword);
        Enter(location);
        var title = SequenceText.Parse(TextOf(Expect(SequenceTokens.RestOfLine)));
        _statements.Add(new SequenceBlockStatement(block.Start, title, location));

        string[] stops = block.Section is null ? [SequenceTokens.End] : [SequenceTokens.End, block.Section];
        ParseDocument(stops);
        while (block.Section is not null && Is(block.Section))
        {
            var section = Advance();
            var text = SequenceText.Parse(TextOf(Expect(SequenceTokens.RestOfLine)));
            _statements.Add(new SequenceBlockStatement(block.SectionType, text, Locate(section)));
            ParseDocument(stops);
        }

        var end = Expect(SequenceTokens.End);
        _statements.Add(new SequenceBlockStatement(block.End, null, Locate(end)));
        Leave();
    }

    /// <summary>
    ///     Parses a signal: a participant, an arrow with its modifiers (<c>+</c>, <c>-</c>, <c>()</c> after it, <c>()</c>
    ///     before it, or both), a participant and a text.
    /// </summary>
    private void ParseSignal()
    {
        var location = Locate(Current);
        var from = ExpectActor();
        string to;
        SequenceText text;
        if (Is(SequenceTokens.Central))
        {
            // A central connection at the source, and maybe at the target
            Advance();
            var type = ExpectArrow();
            var dual = Is(SequenceTokens.Central);
            if (dual)
            {
                Advance();
            }
            else if (!Is(SequenceTokens.Actor))
            {
                throw Fail([SequenceTokens.Central, SequenceTokens.Actor]);
            }

            to = ExpectActor();
            text = ExpectText();
            Expect(SequenceTokens.Newline);
            AddMessage(from, to, type, text, dual, dual ? SequenceLineType.CentralConnectionDual : SequenceLineType.CentralConnectionReverse, location);
            if (dual)
            {
                _statements.Add(new SequenceActorSignalStatement(to, SequenceLineType.CentralConnection, location));
            }

            _statements.Add(new SequenceActorSignalStatement(from, SequenceLineType.CentralConnectionReverse, location));
            return;
        }

        if (!Is(Arrows))
        {
            throw Fail(SignalStarts);
        }

        var arrow = ExpectArrow();
        var modifier = Is(SequenceTokens.Plus, SequenceTokens.Minus, SequenceTokens.Central) ? Advance().Kind : null;
        if (modifier is null && !Is(SequenceTokens.Actor))
        {
            throw Fail(ArrowFollowers);
        }

        to = ExpectActor();
        text = ExpectText();
        Expect(SequenceTokens.Newline);
        switch (modifier)
        {
            case SequenceTokens.Plus:
                AddMessage(from, to, arrow, text, true, 0, location);
                _statements.Add(new SequenceActorSignalStatement(to, SequenceLineType.ActiveStart, location));
                break;
            case SequenceTokens.Minus:
                AddMessage(from, to, arrow, text, false, 0, location);
                _statements.Add(new SequenceActorSignalStatement(from, SequenceLineType.ActiveEnd, location));
                break;
            case SequenceTokens.Central:
                AddMessage(from, to, arrow, text, true, SequenceLineType.CentralConnection, location);
                _statements.Add(new SequenceActorSignalStatement(to, SequenceLineType.CentralConnection, location));
                break;
            default:
                AddMessage(from, to, arrow, text, false, 0, location);
                break;
        }
    }

    /// <summary>
    ///     Lists a message with the mentions of its two participants before it.
    /// </summary>
    /// <param name="from">The source.</param>
    /// <param name="to">The target.</param>
    /// <param name="type">The line type.</param>
    /// <param name="text">The text.</param>
    /// <param name="activate">Whether it activates the target.</param>
    /// <param name="centralConnection">The central connection, or 0.</param>
    /// <param name="location">The location of the statement.</param>
    private void AddMessage(string from, string to, int type, SequenceText text, bool activate, int centralConnection, MermaidLocation location)
    {
        Mention(from, location);
        Mention(to, location);
        _statements.Add(new SequenceMessageStatement(from, to, type, text, activate, centralConnection, location));
    }

    /// <summary>
    ///     Lists the mention of a participant in a statement, which declares it when it is new.
    /// </summary>
    /// <param name="actor">The participant.</param>
    /// <param name="location">The location of the statement.</param>
    private void Mention(string actor, MermaidLocation location) =>
        _statements.Add(new SequenceParticipantStatement(actor, null, null, null, false, location));

    /// <summary>
    ///     Consumes a participant name in a statement (<c>actor</c>) and checks the merged lookahead after it.
    /// </summary>
    /// <returns>The name.</returns>
    private string ExpectActor()
    {
        var name = TextOf(Expect(SequenceTokens.Actor));
        CheckLookahead(ActorFollowers);
        return name;
    }

    /// <summary>
    ///     Consumes an arrow (<c>signaltype</c>) and checks the merged lookahead after it.
    /// </summary>
    /// <returns>The line type of the arrow.</returns>
    private int ExpectArrow()
    {
        if (!SequenceTokens.ArrowLineTypes.TryGetValue(Current.Kind, out var type))
        {
            throw Fail(Arrows);
        }

        Advance();
        CheckLookahead(ArrowFollowers);
        return type;
    }

    /// <summary>
    ///     Consumes the text of a message or note (<c>text2</c>): the token without its colon, read by
    ///     <see cref="SequenceText.Parse"/>.
    /// </summary>
    /// <returns>The text.</returns>
    private SequenceText ExpectText()
    {
        var token = Expect(SequenceTokens.Txt);
        return SequenceText.Parse(Preprocessor.TrimJs(TextOf(token))[1..]);
    }

    /// <summary>
    ///     Reads a number of an <c>autonumber</c> statement as JavaScript's <c>Number</c> does.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <returns>The number.</returns>
    private static double Number(ScanToken token) => double.Parse(token.Text, NumberStyles.Float, CultureInfo.InvariantCulture);
}
