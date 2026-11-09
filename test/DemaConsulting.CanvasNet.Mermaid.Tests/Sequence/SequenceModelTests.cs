using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Mermaid.Configuration;
using DemaConsulting.CanvasNet.Mermaid.Sequence;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Sequence;

/// <summary>
///     Unit tests of <see cref="SequenceModel"/> and its elements: the rules of Mermaid's sequence database for
///     participants, boxes, created and destroyed participants, activations, links and properties, wrap settings, the
///     metadata and the limits.
/// </summary>
public class SequenceModelTests
{
    /// <summary>
    ///     Validates the participants in insertion order with their previous and next participants.
    /// </summary>
    [Fact]
    public void SequenceModel_Apply_Participants_KeepInsertionOrderAndNeighbors()
    {
        // Act
        var model = SequenceFacts.Parse("sequenceDiagram\n    participant Gate\n    Visitor->>Gate: knock\n    Gate->>Guard: call\n");

        // Assert
        Assert.Equal(
            [("Gate", (string?)null, (string?)"Visitor"), ("Visitor", "Gate", "Guard"), ("Guard", "Visitor", null)],
            model.Actors.Select(a => (a.Id, a.PrevActor, a.NextActor)));
        Assert.All(model.Actors, a => Assert.Equal(SequenceParticipantTypes.Participant, a.Type));
    }

    /// <summary>
    ///     Validates that a participant declared again with an alias is replaced in place and becomes its own neighbor,
    ///     while a declaration without an alias changes nothing, as in Mermaid.
    /// </summary>
    [Fact]
    public void SequenceModel_Apply_Redeclared_ReplacesOnlyWithAlias()
    {
        // Act
        var replaced = SequenceFacts.Parse("sequenceDiagram\n    participant P as First\n    participant P as Second\n");
        var kept = SequenceFacts.Parse("sequenceDiagram\n    participant P as Kept\n    actor P\n");

        // Assert
        var p = Assert.Single(replaced.Actors);
        Assert.Equal(("Second", "P", "P"), (p.Description, p.PrevActor, p.NextActor));
        Assert.Equal(("Kept", SequenceParticipantTypes.Participant), (kept.Actors[0].Description, kept.Actors[0].Type));
    }

    /// <summary>
    ///     Validates the metadata rules: an unknown type is kept, a null type keeps the keyword's, and an alias in the
    ///     metadata does not replace an alias after <c>as</c>.
    /// </summary>
    [Fact]
    public void SequenceModel_Apply_Metadata_FollowsMermaid()
    {
        // Act
        var model = SequenceFacts.Parse(
            "sequenceDiagram\n    participant A@{ type: cloud }\n    actor B@{ type: null }\n    participant C@{ alias: Ignored } as Shown\n");

        // Assert
        Assert.Equal(
            [("A", "A", "cloud"), ("B", "B", "actor"), ("C", "Shown", "participant")],
            model.Actors.Select(a => (a.Id, a.Description, a.Type)));
        Assert.False(SequenceParticipantTypes.IsKnown("cloud"));
        Assert.True(SequenceParticipantTypes.IsKnown("queue"));
    }

    /// <summary>
    ///     Validates the message list as Mermaid 11.17.2's sequence database lists it for the same text: the signals with
    ///     their line types, the notes with their placement, the start, section, and end markers of the blocks in the
    ///     order of the text, and the participants named only by messages added after the declared one.
    /// </summary>
    [Fact]
    public void SequenceModel_Apply_Messages_KeepMermaidsListAndAddParticipants()
    {
        // Arrange
        const string Diagram =
            "sequenceDiagram\n    participant A\n    A->>B: hello\n    Note right of B: think\n    loop Every minute\n" +
            "        B-->>A: tick\n    end\n    alt Ok\n        A->>C: go\n    else Failed\n        A-xC: stop\n    end\n" +
            "    Note over A,C: done\n";

        // Act
        var model = SequenceFacts.Parse(Diagram);

        // Assert: the messages in Mermaid's order, ids, participants, texts, line types, and placements
        Assert.Equal(
            [
                ("0", (string?)"A", (string?)"B", "hello", SequenceLineType.Solid, (int?)null),
                ("1", "B", "B", "think", SequenceLineType.Note, SequencePlacement.RightOf),
                ("2", null, null, "Every minute", SequenceLineType.LoopStart, null),
                ("3", "B", "A", "tick", SequenceLineType.Dotted, null),
                ("4", null, null, "", SequenceLineType.LoopEnd, null),
                ("5", null, null, "Ok", SequenceLineType.AltStart, null),
                ("6", "A", "C", "go", SequenceLineType.Solid, null),
                ("7", null, null, "Failed", SequenceLineType.AltElse, null),
                ("8", "A", "C", "stop", SequenceLineType.SolidCross, null),
                ("9", null, null, "", SequenceLineType.AltEnd, null),
                ("10", "A", "C", "done", SequenceLineType.Note, SequencePlacement.Over),
            ],
            model.Messages.Select(m => (m.Id, m.From, m.To, m.Text, m.Type, m.Placement)));

        // Assert: B and C added by the messages, with their neighbors
        Assert.Equal(
            [("A", (string?)null, (string?)"B"), ("B", "A", "C"), ("C", "B", null)],
            model.Actors.Select(a => (a.Id, a.PrevActor, a.NextActor)));
    }

    /// <summary>
    ///     Validates how a box statement splits into a color and a title, as Mermaid's <c>parseBoxData</c> with the
    ///     browser's color check does.
    /// </summary>
    /// <param name="text">The text after <c>box</c>.</param>
    /// <param name="color">The expected color.</param>
    /// <param name="title">The expected title, or null.</param>
    [Theory]
    [InlineData("Aqua Team one", "Aqua", "Team one")]
    [InlineData("Team two", "transparent", "Team two")]
    [InlineData("rgb(200, 10, 10) Red team", "rgb(200, 10, 10)", "Red team")]
    [InlineData("rgba (0, 0, 255, 0.2) Blue team", "transparent", "rgba (0, 0, 255, 0.2) Blue team")]
    [InlineData("hsl(120, 50%, 50%)", "hsl(120, 50%, 50%)", null)]
    [InlineData("Canvas Team", "Canvas", "Team")]
    [InlineData("RGB(1,2,3) Team", "transparent", "RGB(1,2,3) Team")]
    [InlineData("", "transparent", null)]
    public void SequenceModel_ParseBoxData_SplitsColorAndTitle(string text, string color, string? title)
    {
        // Act
        var data = SequenceModel.ParseBoxData(text);

        // Assert
        Assert.Equal((color, title), (data.Color, data.Text));
    }

    /// <summary>
    ///     Validates the boxes: members, titles with wrap prefixes, and the box of each participant.
    /// </summary>
    [Fact]
    public void SequenceModel_Apply_Boxes_KeepMembers()
    {
        // Act
        var model = SequenceFacts.Parse(
            "sequenceDiagram\n    box Aqua wrap:Front\n    participant A\n    participant A as Again\n    end\n    box Back\n    actor B\n    end\n    C->>A: x\n");

        // Assert
        Assert.Equal(
            [("Front", "Aqua", (bool?)true), ("Back", "transparent", null)],
            model.Boxes.Select(b => (b.Name, b.Fill, b.Wrap)));
        Assert.Equal(["A", "A"], model.Boxes[0].ActorKeys);
        Assert.Equal([model.Boxes[0], model.Boxes[1], null], model.Actors.Select(a => a.Box));
        Assert.True(model.HasAtLeastOneBoxWithTitle);
    }

    /// <summary>
    ///     Validates that a participant in two boxes is Mermaid's error, located at the second declaration.
    /// </summary>
    [Fact]
    public void SequenceModel_Apply_ParticipantInTwoBoxes_Throws()
    {
        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => SequenceFacts.Parse(
            "sequenceDiagram\n    box One\n    participant A\n    end\n    box Two\n    participant A\n    end\n"));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Equal(new MermaidLocation(null, 6, 5), ex.Location);
        Assert.EndsWith("A same participant should only be defined in one Box: A can't be in 'One' and in 'Two' at the same time.", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates created and destroyed participants: the index of the message after the statement.
    /// </summary>
    [Fact]
    public void SequenceModel_Apply_CreateAndDestroy_RecordMessageIndexes()
    {
        // Act
        var model = SequenceFacts.Parse(
            "sequenceDiagram\n    A->>B: hi\n    create participant T\n    B->>T: new\n    destroy T\n    T-->>B: gone\n");

        // Assert
        Assert.Equal(1, model.CreatedActors["T"]);
        Assert.Equal(2, model.DestroyedActors["T"]);
    }

    /// <summary>
    ///     Validates Mermaid's errors for <c>create</c> and <c>destroy</c>, with a hint that names the participant.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="message">The end of Mermaid's message.</param>
    /// <param name="participant">The participant the hint names.</param>
    [Theory]
    [InlineData("sequenceDiagram\n    A->>B: hi\n    create participant C\n    A->>B: x\n", "The created participant undefined does not have an associated creating message after its declaration. Please check the sequence diagram.", "C")]
    [InlineData("sequenceDiagram\n    A->>B: hi\n    destroy C\n    A->>B: x\n", "The destroyed participant undefined does not have an associated destroying message after its declaration. Please check the sequence diagram.", "C")]
    [InlineData("sequenceDiagram\n    A->>B: hi\n    create participant B\n    A->>B: x\n", "It is not possible to have actors with the same id, even if one is destroyed before the next is created. Use 'AS' aliases to simulate the behavior", "B")]
    public void SequenceModel_Apply_CreateOrDestroyMisuse_ThrowsMermaidError(string text, string message, string participant)
    {
        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => SequenceFacts.Parse(text));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.SyntaxError, ex.Code);
        Assert.EndsWith(message, ex.Message, StringComparison.Ordinal);
        Assert.Contains($"'{participant}", ex.Hint, StringComparison.Ordinal);
        Assert.True(ex.Location.IsKnown);
    }

    /// <summary>
    ///     Validates that deactivating a participant that is not active is Mermaid's error, while balanced activations
    ///     are accepted.
    /// </summary>
    [Fact]
    public void SequenceModel_Apply_DeactivateInactive_Throws()
    {
        // Act
        var balanced = SequenceFacts.Parse("sequenceDiagram\n    A->>+B: x\n    activate B\n    deactivate B\n    B-->>-A: y\n");
        var ex = Assert.Throws<MermaidSyntaxException>(() => SequenceFacts.Parse("sequenceDiagram\n    A->>+B: x\n    B-->>-A: y\n    deactivate B\n"));

        // Assert
        Assert.Equal(6, balanced.Messages.Count);
        Assert.EndsWith("Trying to inactivate an inactive participant (B)", ex.Message, StringComparison.Ordinal);
        Assert.Equal(new MermaidLocation(null, 4, 5), ex.Location);
    }

    /// <summary>
    ///     Validates the links and properties: <c>link</c> splits at the at sign (with Mermaid's slice when there is none),
    ///     <c>links</c> and <c>properties</c> read JSON, and text that is not JSON is ignored.
    /// </summary>
    [Fact]
    public void SequenceModel_Apply_LinksAndProperties_ReadLikeMermaid()
    {
        // Act
        var model = SequenceFacts.Parse(
            "sequenceDiagram\n    participant A\n    link A: Docs @ https://example.com/?q=a&b=c\n    link A: no at sign xy\n" +
            "    links A: {\"b\": 1, \"2\": \"two\"}\n    links A: {not json}\n    properties A: {\"class\": \"internal\"}\n    details A: id\n");

        // Assert
        var actor = Assert.Single(model.Actors);
        Assert.Equal(["2", "Docs", "no at sign", "b"], actor.Links.Keys);
        Assert.True(actor.Links.TryGetValue("Docs", out var docs));
        Assert.Equal("https://example.com/?q=a&b=c", (string?)docs);
        Assert.True(actor.Links.TryGetValue("no at sign", out var noAt));
        Assert.Equal("no at sign xy", (string?)noAt);
        Assert.True(actor.Properties.TryGetValue("class", out var cssClass));
        Assert.Equal("internal", (string?)cssClass);
        Assert.Equal(1, actor.Properties.Count);
    }

    /// <summary>
    ///     Validates that a JSON array or string given to <c>links</c> adds its indexes as keys, as a JavaScript
    ///     <c>for...in</c> loop does.
    /// </summary>
    [Fact]
    public void SequenceModel_Apply_LinksArrayAndString_AddIndexes()
    {
        // Act
        var model = SequenceFacts.Parse("sequenceDiagram\n    links A: [\"x\", {\"k\": 1}]\n    links B: \"ab\"\n");

        // Assert
        Assert.Equal(["0", "1"], model.Actors[0].Links.Keys);
        Assert.True(model.Actors[0].Links.TryGetValue("1", out var item));
        Assert.Equal("{\"k\":1}", item!.ToJsonString());
        Assert.True(model.Actors[1].Links.TryGetValue("1", out var character));
        Assert.Equal("b", (string?)character);
    }

    /// <summary>
    ///     Validates the wrap settings: an explicit prefix wins, and an unset one follows the configuration.
    /// </summary>
    [Fact]
    public void SequenceModel_EffectiveWrap_ExplicitOrConfiguration()
    {
        // Arrange
        var model = SequenceFacts.Parse("sequenceDiagram\n    A->>B: wrap:on\n    A->>B: nowrap:off\n    A->>B: unset\n");
        var wrapping = new SequenceConfig { Wrap = true };
        var plain = new SequenceConfig();

        // Act
        var withWrap = model.Messages.Select(m => SequenceModel.EffectiveWrap(m.Wrap, wrapping)).ToList();
        var withoutWrap = model.Messages.Select(m => SequenceModel.EffectiveWrap(m.Wrap, plain)).ToList();

        // Assert
        Assert.Equal([true, false, true], withWrap);
        Assert.Equal([true, false, false], withoutWrap);
    }

    /// <summary>
    ///     Validates how a text is read: trimmed, then a <c>wrap:</c> or <c>nowrap:</c> prefix (optionally after a colon,
    ///     case-sensitive) is taken off.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="expected">The text without its prefix.</param>
    /// <param name="wrap">The expected wrap setting.</param>
    [Theory]
    [InlineData("  wrap: Long text ", "Long text", true)]
    [InlineData(":nowrap:Kept", "Kept", false)]
    [InlineData("say wrap:later", "say wrap:later", null)]
    [InlineData("WRAP:upper", "WRAP:upper", null)]
    [InlineData("::wrap:x", "::wrap:x", null)]
    public void SequenceText_Parse_TakesWrapPrefix(string text, string expected, bool? wrap)
    {
        // Act
        var result = SequenceText.Parse(text);

        // Assert
        Assert.Equal(new SequenceText(expected, wrap), result);
    }

    /// <summary>
    ///     Validates the document metadata of the model: title, accessible title and the counts of participants,
    ///     messages drawn as arrows, and boxes.
    /// </summary>
    [Fact]
    public void SequenceModel_Metadata_CountsParticipantsMessagesAndBoxes()
    {
        // Act
        var model = SequenceFacts.Parse(
            "sequenceDiagram\n    title Orders\n    accTitle: Flow\n    box Aqua Team\n    participant A\n    end\n" +
            "    loop Again\n    A->>+B: x\n    end\n    Note over A: n\n    B-->>-A: y\n    autonumber\n");

        // Assert
        Assert.Equal(("Orders", "Flow"), (model.Title, model.AccTitle));
        Assert.Equal((2, 2, 1), (model.NodeCount, model.EdgeCount, model.SubgraphCount));
        Assert.True(SequenceLineType.IsArrow(SequenceLineType.StickArrowBottomReverseDotted));
        Assert.False(SequenceLineType.IsArrow(SequenceLineType.Note));
    }

    /// <summary>
    ///     Validates that a diagram without a title has none, so the frontmatter title can apply.
    /// </summary>
    [Fact]
    public void SequenceModel_Metadata_NoTitle_IsNull()
    {
        // Act
        var model = SequenceFacts.Parse("---\ntitle: Front\n---\nsequenceDiagram\n    A->>B: x\n");

        // Assert
        Assert.Equal(((string?)null, (string?)null), (model.Title, model.AccTitle));
    }

    /// <summary>
    ///     Validates that more participants than <see cref="MermaidLimits.MaxNodes"/> are refused, located at the
    ///     statement that adds one too many.
    /// </summary>
    [Fact]
    public void SequenceModel_Apply_TooManyParticipants_ThrowsLimitExceeded()
    {
        // Arrange
        var limits = new MermaidLimits { MaxNodes = 3 };

        // Act
        var ex = Assert.Throws<MermaidSyntaxException>(() => SequenceFacts.Parse("sequenceDiagram\n    A->>B: x\n    C->>D: y\n", limits: limits));

        // Assert
        Assert.Equal(MermaidDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(new MermaidLocation(null, 3, 5), ex.Location);
    }

    /// <summary>
    ///     Validates the order of the keys of a property map: array indexes first, in numeric order, then the other keys
    ///     in insertion order; a key set again keeps its place.
    /// </summary>
    [Fact]
    public void SequencePropertyMap_Keys_JavaScriptOrder()
    {
        // Arrange
        var map = new SequencePropertyMap();

        // Act
        map.Set("b", JsonValue.Create(1));
        map.Set("10", JsonValue.Create(2));
        map.Set("a", JsonValue.Create(3));
        map.Set("2", JsonValue.Create(4));
        map.Set("01", JsonValue.Create(5));
        map.Set("b", JsonValue.Create(6));

        // Assert
        Assert.Equal(["2", "10", "b", "a", "01"], map.Keys);
        Assert.True(map.TryGetValue("b", out var b));
        Assert.Equal(6, (int)b!);
    }

    /// <summary>
    ///     Validates the math test of Mermaid's <c>hasKatex</c>: two dollar signs, then two more on the same line.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="expected">Whether it has math.</param>
    [Theory]
    [InlineData("$$x^2$$", true)]
    [InlineData("a $$ b $$ c", true)]
    [InlineData("$$$$", true)]
    [InlineData("$$$", false)]
    [InlineData("$$x\n$$", false)]
    [InlineData("$ x $", false)]
    [InlineData("", false)]
    public void SequenceModel_HasKatex_FollowsMermaidsPattern(string text, bool expected)
    {
        // Act / Assert
        Assert.Equal(expected, SequenceModel.HasKatex(text));
    }
}
