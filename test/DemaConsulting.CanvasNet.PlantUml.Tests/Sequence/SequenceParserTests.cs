using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Sequence;
using DemaConsulting.CanvasNet.PlantUml.Styling;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Sequence;

/// <summary>
///     Unit tests for the <c>SequenceParser</c> unit: the statements of a sequence diagram, their errors, their reports,
///     and their limits.
/// </summary>
public class SequenceParserTests
{
    /// <summary>
    ///     Validates the participant declarations: the eight kinds, aliases both ways, quoted names, colors and order.
    /// </summary>
    [Fact]
    public void PlantUmlSequenceParser_Declarations_ReadKindsAliasesColorsAndOrder()
    {
        // Arrange
        const string body = "actor User\nboundary B\ncontrol C\nentity E\ndatabase D\ncollections Q\nqueue J\n" +
                            "participant \"Long Name\" as L #FFEECC\nparticipant S as \"Short\"\nparticipant First order -1\n";

        // Act
        var model = SequenceTestHelpers.Parse(body);

        // Assert
        Assert.Equal("First", model.Participants[0].Code);
        Assert.Equal(
            [ParticipantKind.Participant, ParticipantKind.Actor, ParticipantKind.Boundary, ParticipantKind.Control, ParticipantKind.Entity, ParticipantKind.Database, ParticipantKind.Collections, ParticipantKind.Queue],
            model.Participants.Take(8).Select(p => p.Kind));
        var aliased = model.Participants.Single(p => p.Code == "L");
        Assert.Equal("Long Name", aliased.Display);
        Assert.Equal(new Canvas.Rgba32(0xFF, 0xEE, 0xCC, 255), aliased.Fill);
        Assert.Equal("Short", model.Participants.Single(p => p.Code == "S").Display);
        Assert.Equal(-1, model.Participants[0].Order);
    }

    /// <summary>
    ///     Validates that a participant used before it is declared is declared implicitly, and a later declaration
    ///     changes it in place.
    /// </summary>
    [Fact]
    public void PlantUmlSequenceParser_ImplicitParticipant_LaterDeclarationUpdatesIt()
    {
        // Act
        var model = SequenceTestHelpers.Parse("A -> B : hi\nactor B");

        // Assert
        Assert.Equal(2, model.Participants.Count);
        Assert.Equal(ParticipantKind.Actor, model.Participants[1].Kind);
    }

    /// <summary>
    ///     Validates the arrows: bodies, heads, halves, crosses, circles, colors, and both directions.
    /// </summary>
    /// <param name="arrow">The arrow text.</param>
    /// <param name="dotted">The expected body.</param>
    /// <param name="fromHead">The expected head at the sender.</param>
    /// <param name="toHead">The expected head at the receiver.</param>
    /// <param name="reversed">Whether the right participant sends.</param>
    [Theory]
    [InlineData("->", false, "None", "Filled", false)]
    [InlineData("-->", true, "None", "Filled", false)]
    [InlineData("->>", false, "None", "Open", false)]
    [InlineData("-->>", true, "None", "Open", false)]
    [InlineData("->x", false, "None", "Cross", false)]
    [InlineData("-\\", false, "None", "HalfTop", false)]
    [InlineData("-/", false, "None", "HalfBottom", false)]
    [InlineData("<-", false, "None", "Filled", true)]
    [InlineData("<--", true, "None", "Filled", true)]
    [InlineData("<->", false, "Filled", "Filled", false)]
    [InlineData("-[#red]>", false, "None", "Filled", false)]
    public void PlantUmlSequenceParser_Arrows_ReadBodyHeadsAndDirection(string arrow, bool dotted, string fromHead, string toHead, bool reversed)
    {
        // Act
        var model = SequenceTestHelpers.Parse($"participant A\nparticipant B\nA {arrow} B : m");

        // Assert
        var message = Assert.IsType<SequenceMessage>(model.Events.Single());
        Assert.Equal(dotted, message.Arrow.Dotted);
        Assert.Equal(Enum.Parse<ArrowHead>(fromHead), message.Arrow.FromHead);
        Assert.Equal(Enum.Parse<ArrowHead>(toHead), message.Arrow.ToHead);
        Assert.Equal(reversed ? 1 : 0, message.From);
        Assert.Equal("m", message.Label);
    }

    /// <summary>
    ///     Validates the circle decoration and the color of an arrow.
    /// </summary>
    [Fact]
    public void PlantUmlSequenceParser_Arrows_CircleAndColor()
    {
        // Act
        var model = SequenceTestHelpers.Parse("A ->o B\nA -[#0000FF]> B");

        // Assert
        var messages = model.Events.OfType<SequenceMessage>().ToList();
        Assert.True(messages[0].Arrow.ToCircle);
        Assert.Equal(string.Empty, messages[0].Label);
        Assert.Equal(new Canvas.Rgba32(0, 0, 0xFF, 255), messages[1].Arrow.Color);
    }

    /// <summary>
    ///     Validates the activation shorthands and the activate, deactivate and destroy statements, and which of them
    ///     happen on the arrow of the message before them.
    /// </summary>
    [Fact]
    public void PlantUmlSequenceParser_LifeChanges_AttachToTheirMessage()
    {
        // Act
        var model = SequenceTestHelpers.Parse("activate A\nA -> B ++ : go\nB --> A -- : back\nactivate A\nA -> B !! : end\ndeactivate A");

        // Assert
        Assert.IsType<SequenceLifeEvent>(model.Events[0]);
        var messages = model.Events.OfType<SequenceMessage>().ToList();
        Assert.Equal([new LifeChange(1, LifeAction.Activate, null)], messages[0].Changes);
        Assert.Equal([new LifeChange(1, LifeAction.Deactivate, null), new LifeChange(0, LifeAction.Activate, null)], messages[1].Changes);
        Assert.Equal([new LifeChange(1, LifeAction.Destroy, null), new LifeChange(0, LifeAction.Deactivate, null)], messages[2].Changes);
    }

    /// <summary>
    ///     Validates that return replies to the sender of the innermost activating message with a dotted arrow and
    ///     ends the activation, and that a return with nothing to return to fails.
    /// </summary>
    [Fact]
    public void PlantUmlSequenceParser_Return_RepliesAndDeactivates()
    {
        // Act
        var model = SequenceTestHelpers.Parse("A -> B ++ : ask\nreturn answer");
        var ex = Assert.Throws<PlantUmlSyntaxException>(() => SequenceTestHelpers.Parse("participant A\nreturn x"));

        // Assert
        var reply = model.Events.OfType<SequenceMessage>().Last();
        Assert.Equal((1, 0), (reply.From, reply.To));
        Assert.True(reply.Arrow.Dotted);
        Assert.Equal("answer", reply.Label);
        Assert.Contains(new LifeChange(1, LifeAction.Deactivate, null), reply.Changes);
        Assert.Equal(PlantUmlDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Equal(3, ex.Location.Line);
    }

    /// <summary>
    ///     Validates autonumber: the default bold numbers, a start and a step, a format, stop and resume.
    /// </summary>
    [Fact]
    public void PlantUmlSequenceParser_Autonumber_NumbersMessages()
    {
        // Act
        var model = SequenceTestHelpers.Parse(
            "autonumber\nA -> B\nautonumber 10 5 \"<b>[000]\"\nA -> B\nA -> B\nautonumber stop\nA -> B\nautonumber resume\nA -> B");

        // Assert
        var numbers = model.Events.OfType<SequenceMessage>().Select(m => m.Number).ToList();
        Assert.Equal(["<b>1", "<b>[010]", "<b>[015]", null, "<b>[020]"], numbers);
    }

    /// <summary>
    ///     Validates the notes: positions, participants, on the row of a message, across, multi-line and shapes.
    /// </summary>
    [Fact]
    public void PlantUmlSequenceParser_Notes_ReadPositionsShapesAndText()
    {
        // Act
        var model = SequenceTestHelpers.Parse(
            "A -> B : m\nnote right : on the row\nnote left of A #Pink : left\nnote over A, B : both\n" +
            "hnote over B : hex\nrnote over A : box\nnote across : all\nnote over A\nline one\n\nline three\nend note");

        // Assert
        var notes = model.Events.OfType<SequenceNote>().ToList();
        Assert.True(notes[0].OnMessage);
        Assert.Equal(NotePosition.Right, notes[0].Position);
        Assert.Equal((NotePosition.Left, 0, 0, "left"), (notes[1].Position, notes[1].First, notes[1].Last, notes[1].Text));
        Assert.NotNull(notes[1].Fill);
        Assert.Equal((NotePosition.Over, 0, 1), (notes[2].Position, notes[2].First, notes[2].Last));
        Assert.Equal(NoteShape.Hexagon, notes[3].Shape);
        Assert.Equal(NoteShape.Box, notes[4].Shape);
        Assert.Equal((0, 1), (notes[5].First, notes[5].Last));
        Assert.Equal("line one\n\nline three", notes[6].Text);
    }

    /// <summary>
    ///     Validates the groups: keywords, conditions, colors, group titles, else and end.
    /// </summary>
    [Fact]
    public void PlantUmlSequenceParser_Groups_ReadKeywordsConditionsAndSections()
    {
        // Act
        var model = SequenceTestHelpers.Parse(
            "alt #LightBlue success\nA -> B\nelse failure\nA -> B\nend\nloop\nA -> B\nend\ngroup Title [detail]\nA -> B\nend");

        // Assert
        var starts = model.Events.OfType<SequenceGroupStart>().ToList();
        Assert.Equal(("alt", "success"), (starts[0].Keyword, starts[0].Label));
        Assert.NotNull(starts[0].Fill);
        Assert.Equal(("loop", null), (starts[1].Keyword, starts[1].Label));
        Assert.Equal(("Title ", "detail"), (starts[2].Keyword, starts[2].Label));
        Assert.Equal("failure", model.Events.OfType<SequenceGroupElse>().Single().Label);
        Assert.Equal(3, model.Events.OfType<SequenceGroupEnd>().Count());
    }

    /// <summary>
    ///     Validates the errors of groups and boxes: else and end outside a group, an unclosed group, a nested box, an
    ///     unclosed box, and an end that closes nothing.
    /// </summary>
    /// <param name="body">The statements.</param>
    /// <param name="line">The expected line of the error.</param>
    [Theory]
    [InlineData("A -> B\nelse x", 3)]
    [InlineData("A -> B\nend", 3)]
    [InlineData("alt x\nA -> B", 2)]
    [InlineData("box\nparticipant A\nbox\nend box\nend box", 4)]
    [InlineData("box \"b\"\nparticipant A", 2)]
    [InlineData("A -> B\nend box", 3)]
    [InlineData("A -> B\nend nothing", 3)]
    public void PlantUmlSequenceParser_Blocks_InvalidFailLocated(string body, int line)
    {
        // Act
        var ex = Assert.Throws<PlantUmlSyntaxException>(() => SequenceTestHelpers.Parse(body));

        // Assert
        Assert.Equal(PlantUmlDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Equal(line, ex.Location.Line);
    }

    /// <summary>
    ///     Validates the boxes: titles quoted or not, colors, and their participants.
    /// </summary>
    [Fact]
    public void PlantUmlSequenceParser_Boxes_ReadTitleColorAndMembers()
    {
        // Act
        var model = SequenceTestHelpers.Parse("box \"Front\" #LightBlue\nparticipant A\nparticipant B\nend box\nbox Back end #Pink\nparticipant C\nendbox\nparticipant D");

        // Assert
        Assert.Equal(2, model.Boxes.Count);
        Assert.Equal(("Front", 0, 1), (model.Boxes[0].Title, model.Boxes[0].First, model.Boxes[0].Last));
        Assert.NotNull(model.Boxes[0].Fill);
        Assert.Equal(("Back end", 2, 2), (model.Boxes[1].Title, model.Boxes[1].First, model.Boxes[1].Last));
        Assert.NotNull(model.Boxes[1].Fill);
    }

    /// <summary>
    ///     Validates the dividers, delays and spaces, and that a space that is not a number fails.
    /// </summary>
    [Fact]
    public void PlantUmlSequenceParser_Separators_ReadDividersDelaysAndSpaces()
    {
        // Act
        var model = SequenceTestHelpers.Parse("A -> B\n== Step ==\n...\n... later ...\n|||\n||45||");
        var ex = Assert.Throws<PlantUmlSyntaxException>(() => SequenceTestHelpers.Parse("A -> B\n||x||"));

        // Assert
        Assert.Equal("Step", Assert.IsType<SequenceDivider>(model.Events[1]).Text);
        Assert.Equal(string.Empty, Assert.IsType<SequenceDelay>(model.Events[2]).Text);
        Assert.Equal("later", Assert.IsType<SequenceDelay>(model.Events[3]).Text);
        Assert.Equal(25, Assert.IsType<SequenceSpace>(model.Events[4]).Height);
        Assert.Equal(45, Assert.IsType<SequenceSpace>(model.Events[5]).Height);
        Assert.Equal(3, ex.Location.Line);
    }

    /// <summary>
    ///     Validates the page texts: title, header and footer with their alignments, caption, and legend blocks.
    /// </summary>
    [Fact]
    public void PlantUmlSequenceParser_PageTexts_ReadTextsAndAlignments()
    {
        // Act
        var model = SequenceTestHelpers.Parse(
            "title The Title\nleft header Head\nfooter\nfoot line\nend footer\ncaption Cap\nlegend left\nLeg\nend legend\nA -> B");

        // Assert
        Assert.Equal("The Title", model.Title);
        Assert.Equal(("Head", TextAlign.Left), (model.Page.Header!.Text, model.Page.Header.Align));
        Assert.Equal(("foot line", TextAlign.Center), (model.Page.Footer!.Text, model.Page.Footer.Align));
        Assert.Equal("Cap", model.Page.Caption!.Text);
        Assert.Equal(("Leg", TextAlign.Left), (model.Page.Legend!.Text, model.Page.Legend.Align));
    }

    /// <summary>
    ///     Validates hide footbox, scale, and skin parameters.
    /// </summary>
    [Fact]
    public void PlantUmlSequenceParser_Settings_ApplyFootboxScaleAndSkin()
    {
        // Act
        var model = SequenceTestHelpers.Parse("hide footbox\nscale 3/2\nskinparam participantBackgroundColor #FF0000\nA -> B");

        // Assert
        Assert.False(model.Theme.Footbox);
        Assert.Equal(1.5, model.Scale);
        Assert.Equal(new Canvas.Rgba32(0xFF, 0, 0, 255), model.Theme[SkinElement.Participant].Fill);
    }

    /// <summary>
    ///     Validates that statements PlantUML accepts but this version does not draw are reported and skipped.
    /// </summary>
    [Fact]
    public void PlantUmlSequenceParser_UnsupportedStatements_AreReported()
    {
        // Act
        var model = SequenceTestHelpers.Parse(
            "participant A <<stereo>>\nA -> B ** : make\nref over A, B : see other\nnewpage\n[-> A : in\nhide unlinked\nscale 200 width\nref over A\nbody\nend ref\nA -> B\n& B -> A : together");

        // Assert
        Assert.Equal(9, model.Reports.Count(r => r.Code == PlantUmlDiagnosticCodes.StatementUnsupported));
        Assert.Equal(3, model.Events.OfType<SequenceMessage>().Count());
    }

    /// <summary>
    ///     Validates that messages to or from the diagram border are reported at their lines in every form (a circle or
    ///     a cross at the border, a lost message to '?' with a label), and that a message whose label holds brackets and
    ///     arrows is drawn.
    /// </summary>
    [Fact]
    public void PlantUmlSequenceParser_BorderMessages_AreReported()
    {
        // Act
        var model = SequenceTestHelpers.Parse("A -> B\n[o-> A\nA ->x] : y\nA ->? : r\nA ->o? : s\n?o-> A\nA -> B : see [-x] and x->]");

        // Assert
        Assert.Equal(
            [3, 4, 5, 6, 7],
            model.Reports.Where(r => r.Code == PlantUmlDiagnosticCodes.StatementUnsupported).Select(r => r.Location.Line));
        var messages = model.Events.OfType<SequenceMessage>().ToList();
        Assert.Equal(2, messages.Count);
        Assert.Equal("see [-x] and x->]", messages[1].Label);
    }

    /// <summary>
    ///     Validates that invalid statements fail at their statements with a hint: an unknown statement, a message
    ///     without a receiver or with text after it, incomplete declarations, and invalid or unclosed notes.
    /// </summary>
    /// <param name="body">The statements.</param>
    /// <param name="line">The expected line.</param>
    [Theory]
    [InlineData("A -> B\nfoo bar baz", 3)]
    [InlineData("A -> B\nA -> : x", 3)]
    [InlineData("A -> B\nA -> B extra : x", 3)]
    [InlineData("A -> B\nparticipant", 3)]
    [InlineData("A -> B\nparticipant X order", 3)]
    [InlineData("A -> B\nparticipant X #bogus", 3)]
    [InlineData("A -> B\nactivate", 3)]
    [InlineData("participant A\nnote up of A : x", 3)]
    [InlineData("participant A\nnote left : x", 3)]
    [InlineData("participant A\nnote over A\ntext", 3)]
    public void PlantUmlSequenceParser_InvalidStatement_FailsLocated(string body, int line)
    {
        // Act
        var ex = Assert.Throws<PlantUmlSyntaxException>(() => SequenceTestHelpers.Parse(body));

        // Assert
        Assert.Equal(PlantUmlDiagnosticCodes.SyntaxError, ex.Code);
        Assert.Equal(line, ex.Location.Line);
        Assert.NotNull(ex.Hint);
    }

    /// <summary>
    ///     Validates the limits on participants, messages, text length and group nesting.
    /// </summary>
    [Fact]
    public void PlantUmlSequenceParser_Limits_AreEnforced()
    {
        // Act
        var nodes = Assert.Throws<PlantUmlSyntaxException>(() => SequenceTestHelpers.Parse("A -> B\nB -> C", new PlantUmlLimits { MaxNodes = 2 }));
        var edges = Assert.Throws<PlantUmlSyntaxException>(() => SequenceTestHelpers.Parse("A -> B\nB -> A", new PlantUmlLimits { MaxEdges = 1 }));
        var label = Assert.Throws<PlantUmlSyntaxException>(() => SequenceTestHelpers.Parse("A -> B : " + new string('x', 20), new PlantUmlLimits { MaxLabelLength = 10 }));
        var depth = Assert.Throws<PlantUmlSyntaxException>(() => SequenceTestHelpers.Parse("alt\nalt\nA -> B\nend\nend", new PlantUmlLimits { MaxNestingDepth = 1 }));

        // Assert
        Assert.All([nodes, edges, label, depth], ex => Assert.Equal(PlantUmlDiagnosticCodes.LimitExceeded, ex.Code));
        Assert.Equal(3, nodes.Location.Line);
        Assert.Equal(3, edges.Location.Line);
        Assert.Equal(3, depth.Location.Line);
    }
}
