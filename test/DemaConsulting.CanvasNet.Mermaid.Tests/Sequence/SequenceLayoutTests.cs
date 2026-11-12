using DemaConsulting.CanvasNet.Mermaid.Scenegraph;
using DemaConsulting.CanvasNet.Mermaid.Sequence;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Sequence;

/// <summary>
///     Unit tests of the SequenceLayout class: the participant margins, the vertical placement of messages, the notes,
///     the block frames and rect backgrounds, the boxes, the menus, the view box, the title, the mirrored participants,
///     hidden participants, created and destroyed participants, activations, and the work limit.
/// </summary>
public class SequenceLayoutTests
{
    /// <summary>
    ///     Validates the layout of one message between two participants, as Mermaid computes it: participants 150 wide
    ///     and 50 apart, the line 44 below the participants, the mirrored participants 20 below the last message, and the
    ///     view box with the diagram margins.
    /// </summary>
    [Fact]
    public void SequenceLayout_Draw_OneMessage_FollowsMermaidsMeasures()
    {
        // Act
        var scene = SceneQuery.Layout("sequenceDiagram\n    A->>B: x\n");
        var items = SceneQuery.SequenceItems(scene);

        // Assert: the participants, the mirrored ones, the line and the view box
        Assert.Equal(new SceneRect(0, 0, 150, 65), Box(items.Find("actor-top", "A")));
        Assert.Equal(new SceneRect(200, 0, 150, 65), Box(items.Find("actor-top", "B")));
        Assert.Equal(new SceneRect(200, 129, 150, 65), Box(items.Find("actor-bottom", "B")));
        var line = Line(items, "i0");
        Assert.Equal((76.0, 109.0, 271.0, 109.0), (line.Segments[0].X, line.Segments[0].Y, line.Segments[^1].X, line.Segments[^1].Y));
        Assert.Equal(new SceneRect(-50, -10, 450, 215), scene.ViewBox);
    }

    /// <summary>
    ///     Validates that a message wider than the gap between neighbors moves the next participant right so the text fits
    ///     with the wrap padding, while a message between participants that are not neighbors moves nothing.
    /// </summary>
    [Fact]
    public void SequenceLayout_Draw_WideMessage_WidensOnlyTheNeighborsMargin()
    {
        // Arrange
        const string Wide = "a message that is a lot wider than the gap between the two participants";

        // Act
        var neighbors = SceneQuery.SequenceItems(SceneQuery.Layout($"sequenceDiagram\n    A->>B: {Wide}\n"));
        var apart = SceneQuery.SequenceItems(SceneQuery.Layout($"sequenceDiagram\n    participant A\n    participant C\n    participant B\n    A->>B: {Wide}\n"));

        // Assert: the text fits between the lifelines of the neighbors; the far message keeps the default margins
        var text = neighbors.WithRole("message-text").OfType<SceneText>().Single();
        var b = Box(neighbors.Find("actor-top", "B"));
        Assert.True(b.X > 200);
        Assert.True(text.Box.Width + 20 <= b.X + 0.5);
        Assert.Equal(400, Box(apart.Find("actor-top", "B")).X);
    }

    /// <summary>
    ///     Validates the title: 40 more pixels at the top of the view box, and the text at y -25.
    /// </summary>
    [Fact]
    public void SequenceLayout_Draw_Title_AddsRoomAboveTheDiagram()
    {
        // Act
        var plain = SceneQuery.Layout("sequenceDiagram\n    A->>B: x\n");
        var titled = SceneQuery.Layout("sequenceDiagram\n    title Talk\n    A->>B: x\n");
        var frontmatter = SceneQuery.Layout("---\ntitle: From the frontmatter\n---\nsequenceDiagram\n    A->>B: x\n");

        // Assert
        Assert.Equal(new SceneRect(-50, -50, 450, plain.ViewBox.Height + 40), titled.ViewBox);
        var title = Assert.IsType<SceneText>(SceneQuery.OfRole(titled, "title").Single());
        Assert.Equal((75.0, -25.0, "Talk"), (title.X, title.Baseline, title.Runs[0].Text));
        Assert.Equal("From the frontmatter", ((SceneText)SceneQuery.OfRole(frontmatter, "title").Single()).Runs[0].Text);
    }

    /// <summary>
    ///     Validates that a title statement that sanitizes to empty replaces the frontmatter title, so no title or room is
    ///     added, as in Mermaid.
    /// </summary>
    [Fact]
    public void SequenceLayout_Draw_TitleSanitizedToEmpty_AddsNoRoom()
    {
        // Act
        var scene = SceneQuery.Layout("---\ntitle: FM\n---\nsequenceDiagram\n    title <script>x</script>\n    A->>B: x\n");

        // Assert
        Assert.Equal(new SceneRect(-50, -10, 450, 215), scene.ViewBox);
        Assert.Empty(SceneQuery.OfRole(scene, "title"));
    }

    /// <summary>
    ///     Validates that without mirrored participants there is no bottom row, the lifelines run to y 2000, and the view
    ///     box ends at the diagram margin below the last message.
    /// </summary>
    [Fact]
    public void SequenceLayout_Draw_MirrorActorsOff_LifelinesRunTo2000()
    {
        // Act
        var scene = SceneQuery.Layout("---\nconfig:\n  sequence:\n    mirrorActors: false\n---\nsequenceDiagram\n    A->>B: x\n");
        var items = SceneQuery.SequenceItems(scene);

        // Assert
        Assert.Empty(items.WithRole("actor-bottom"));
        var lifeline = (ScenePath)items.Find("lifeline", "A")!;
        Assert.Equal(2000, lifeline.Segments[1].Y);
        Assert.Equal(129, scene.ViewBox.Height);
    }

    /// <summary>
    ///     Validates that hidden unused participants are left out of the drawing and of the margins.
    /// </summary>
    [Fact]
    public void SequenceLayout_Draw_HideUnusedParticipants_LeavesThemOut()
    {
        // Act
        var items = SceneQuery.SequenceItems(SceneQuery.Layout(
            "---\nconfig:\n  sequence:\n    hideUnusedParticipants: true\n---\nsequenceDiagram\n    participant A\n    participant Idle\n    participant B\n    A->>B: x\n"));

        // Assert
        Assert.Null(items.Find("actor-top", "Idle"));
        Assert.Equal(200, Box(items.Find("actor-top", "B")).X);
    }

    /// <summary>
    ///     Validates that a self message is a loop to the right of the lifeline, and a rectangle with right angles.
    /// </summary>
    [Fact]
    public void SequenceLayout_Draw_SelfMessage_IsALoopOrRightAngles()
    {
        // Act
        var curve = Line(SceneQuery.SequenceItems(SceneQuery.Layout("sequenceDiagram\n    A->>A: x\n")), "i0");
        var square = Line(SceneQuery.SequenceItems(SceneQuery.Layout(
            "---\nconfig:\n  sequence:\n    rightAngles: true\n---\nsequenceDiagram\n    A->>A: x\n")), "i0");

        // Assert: Mermaid's cubic from the lifeline back to 20 below it, and the H V H path 25 high
        Assert.Equal((76.0, 109.0), (curve.Segments[0].X, curve.Segments[0].Y));
        Assert.Equal(SceneSegment.Cubic(136, 99, 136, 139, 76, 129), curve.Segments[1]);
        Assert.Equal((76.0, 99.0), (square.Segments[0].X, square.Segments[0].Y));
        Assert.Equal([SceneSegmentKind.MoveTo, SceneSegmentKind.LineTo, SceneSegmentKind.LineTo, SceneSegmentKind.LineTo], square.Segments.Select(s => s.Kind));
        Assert.Equal(25, square.Segments[2].Y - square.Segments[1].Y);
        Assert.Equal(151, square.Segments[1].X);
    }

    /// <summary>
    ///     Validates the activations: an activation starts 2 below the position of its start, stacked activations move right
    ///     by half the activation width, and the line of a message to an activation ends at its side.
    /// </summary>
    [Fact]
    public void SequenceLayout_Draw_Activations_StackAndMoveTheLines()
    {
        // Act
        var items = SceneQuery.SequenceItems(SceneQuery.Layout(
            "sequenceDiagram\n    A->>+B: one\n    A->>+B: two\n    B-->>-A: three\n    B-->>-A: four\n"));
        var activations = items.WithRole("activation").Select(item => Box(item)).ToList();

        // Assert: the outer activation at the lifeline, the inner one 5 to the right, and the second line ends at the outer one
        Assert.Equal(2, activations.Count);
        Assert.Equal((270.0, 280.0), (activations[0].X, activations[0].Right));
        Assert.Equal((275.0, 285.0), (activations[1].X, activations[1].Right));
        Assert.Equal(267, Line(items, "i2").Segments[^1].X);
    }

    /// <summary>
    ///     Validates that a created participant is drawn at its creating message, whose line ends at the side of its box,
    ///     and that a destroyed participant's lifeline ends at its destroying message.
    /// </summary>
    [Fact]
    public void SequenceLayout_Draw_CreateAndDestroy_MoveTheParticipants()
    {
        // Act
        var items = SceneQuery.SequenceItems(SceneQuery.Layout(
            "sequenceDiagram\n    A->>B: hello\n    create participant C\n    B->>C: make\n    destroy C\n    C-->>B: done\n    A->>B: bye\n"));

        // Assert: C is half its width further right, its box is centered on its creating line, and the line stops at the
        // center less the lifeline, the arrowhead and half the box with 3 more (7 before the box)
        var top = Box(items.Find("actor-top", "C"));
        var create = Line(items, "i1");
        Assert.Equal(475, top.X);
        Assert.Equal(top.Y + (top.Height / 2), create.Segments[0].Y);
        Assert.Equal(top.X - 7, create.Segments[^1].X);

        // Assert: the mirrored C is drawn half a box above its destroying line, where its lifeline ends
        var destroy = Line(items, "i2");
        var bottom = Box(items.Find("actor-bottom", "C"));
        Assert.Equal(destroy.Segments[0].Y - (bottom.Height / 2), bottom.Y);
        Assert.Equal(bottom.Y, ((ScenePath)items.Find("lifeline", "C")!).Segments[1].Y);
    }

    /// <summary>
    ///     Validates the sequence numbers: they start at 1 with <c>autonumber</c>, follow a start and step, are not drawn
    ///     after <c>autonumber off</c>, and round to hundredths.
    /// </summary>
    [Fact]
    public void SequenceLayout_Draw_Autonumber_FollowsStartStepAndOff()
    {
        // Act
        var items = SceneQuery.SequenceItems(SceneQuery.Layout(
            "sequenceDiagram\n    autonumber\n    A->>B: a\n    autonumber 10 5\n    A->>B: b\n    A->>B: c\n    autonumber off\n    A->>B: d\n    autonumber 0.1 0.2\n    A->>B: e\n    A->>B: f\n"));
        var numbers = items.WithRole("sequence-number-text").OfType<SceneText>().Select(text => text.Runs[0].Text).ToList();

        // Assert
        Assert.Equal(["1", "10", "15", "0.1", "0.3"], numbers);
    }

    /// <summary>
    ///     Validates that the numbers of <c>showSequenceNumbers</c> are drawn without <c>autonumber</c>.
    /// </summary>
    [Fact]
    public void SequenceLayout_Draw_ShowSequenceNumbers_NumbersEveryMessage()
    {
        // Act
        var items = SceneQuery.SequenceItems(SceneQuery.Layout(
            "---\nconfig:\n  sequence:\n    showSequenceNumbers: true\n---\nsequenceDiagram\n    A->>B: a\n    B-->>A: b\n"));

        // Assert
        Assert.Equal(2, items.WithRole("sequence-number").Count());
    }

    /// <summary>
    ///     Validates the helpers of Mermaid's <c>common.getMax</c> and <c>getMin</c>: missing values and NaN are left out.
    /// </summary>
    [Fact]
    public void SequenceLayout_JsMaxAndJsMin_LeaveOutMissingValues()
    {
        // Act / Assert
        Assert.Equal(3, SequenceLayout.JsMax(1, null, double.NaN, 3));
        Assert.Equal(1, SequenceLayout.JsMin(1, null, double.NaN, 3));
        Assert.Equal(double.NegativeInfinity, SequenceLayout.JsMax((double?)null));
    }

    /// <summary>
    ///     Validates that a measured message ends Mermaid's stateful break search, as its first measurement of a text
    ///     does: a later wrapped message with a break is kept as written, so the participants are where Mermaid draws them
    ///     (B at x 335, from the reference renderer) whatever the message before it.
    /// </summary>
    [Fact]
    public void SequenceLayout_Draw_WrapAfterMeasuredBreak_KeepsTheLaterBreak()
    {
        // Arrange: the first message's break leaves the search past the end of the second message
        const string Diagram =
            "%%{init: {\"sequence\": {\"wrap\": true}}}%%\nsequenceDiagram\nparticipant A\nparticipant B\nparticipant C\nparticipant D\n" +
            "C->>D: ccccccccccccccccccccccccccccccccccccccccccccccccccccc<br>c\nA->>B: a<br>b c d e f g h i j k l m n o p q r s t u v w\n";

        // Act
        var items = SceneQuery.SequenceItems(SceneQuery.Layout(Diagram));

        // Assert
        Assert.Equal(335, Box(items.Find("actor-top", "B")).X);
    }

    /// <summary>
    ///     Validates the notes beside, over, and across the participants, as Mermaid 11.17.2 places them (the values are
    ///     the reference renderer's, which draws the same text): a note left of and right of A beside its lifeline, a note
    ///     over B centered on it, notes over A and B in either order across both lifelines, and a wide note over A that
    ///     widens the margin between A and B.
    /// </summary>
    [Fact]
    public void SequenceLayout_Draw_Notes_BesideOverAndAcrossParticipants()
    {
        // Arrange
        const string Diagram =
            "sequenceDiagram\n    participant A\n    participant B\n    Note left of A: Left\n    Note right of A: Right\n" +
            "    Note over B: Over one\n    Note over A,B: Over both\n    Note over B,A: Reversed\n" +
            "    Note over A: A note over one participant that is wider than the default width\n";

        // Act
        var scene = SceneQuery.Layout(Diagram);
        var items = SceneQuery.SequenceItems(scene);

        // Assert: each note below the one before it, and B moved right by the wide note (x 200 without it)
        Assert.Equal(new SceneRect(-100, 75, 150, 37), NoteBox(items, "i0"));
        Assert.Equal(new SceneRect(100, 122, 150, 37), NoteBox(items, "i1"));
        Assert.Equal(new SceneRect(279, 169, 150, 37), NoteBox(items, "i2"));
        Assert.Equal(new SceneRect(50, 216, 329, 37), NoteBox(items, "i3"));
        Assert.Equal(new SceneRect(50, 263, 329, 37), NoteBox(items, "i4"));
        Assert.Equal(new SceneRect(-154, 310, 458, 37), NoteBox(items, "i5"));
        Assert.Equal(279, Box(items.Find("actor-top", "B")).X);
        Assert.Equal(new SceneRect(-204, -10, 683, 453), scene.ViewBox);
    }

    /// <summary>
    ///     Validates that the noteAlign setting anchors the note text at the left or right padding of the note, as Mermaid
    ///     11.17.2 does (the values are the reference renderer's, which draws the same text), without moving the notes.
    /// </summary>
    [Fact]
    public void SequenceLayout_Draw_NoteAlign_AnchorsTheNoteText()
    {
        // Arrange
        const string Body = "sequenceDiagram\n    participant A\n    participant B\n";

        // Act
        var left = SceneQuery.SequenceItems(SceneQuery.Layout(
            "---\nconfig:\n  sequence:\n    noteAlign: left\n---\n" + Body + "    Note over A,B: Left aligned\n    Note right of B: Short\n"));
        var right = SceneQuery.SequenceItems(SceneQuery.Layout(
            "---\nconfig:\n  sequence:\n    noteAlign: right\n---\n" + Body + "    Note over A,B: Right aligned\n    Note right of B: Short\n"));

        // Assert: the same notes, with the text at the left or the right padding
        Assert.Equal(new SceneRect(50, 75, 250, 37), NoteBox(left, "i0"));
        Assert.Equal(new SceneRect(300, 122, 150, 37), NoteBox(left, "i1"));
        Assert.Equal(new SceneRect(50, 75, 250, 37), NoteBox(right, "i0"));
        Assert.Equal(new SceneRect(300, 122, 150, 37), NoteBox(right, "i1"));
        Assert.Equal((60.0, SceneTextAnchor.Start), (NoteText(left, "i0").X, NoteText(left, "i0").Anchor));
        Assert.Equal((310.0, SceneTextAnchor.Start), (NoteText(left, "i1").X, NoteText(left, "i1").Anchor));
        Assert.Equal((290.0, SceneTextAnchor.End), (NoteText(right, "i0").X, NoteText(right, "i0").Anchor));
        Assert.Equal((440.0, SceneTextAnchor.End), (NoteText(right, "i1").X, NoteText(right, "i1").Anchor));
    }

    /// <summary>
    ///     Validates the frames of a loop and an alt nested in it, as Mermaid 11.17.2 draws them (the values are the
    ///     reference renderer's, which draws the same text): the alt frame inside the loop frame with Mermaid's box
    ///     margins, and the alt frame divided at its else section.
    /// </summary>
    [Fact]
    public void SequenceLayout_Draw_NestedLoopAndAlt_FramesEncloseTheirSections()
    {
        // Arrange
        const string Diagram =
            "sequenceDiagram\n    participant A\n    participant B\n    loop Outer\n        A->>B: one\n        alt Yes\n" +
            "            B->>A: two\n        else No\n            B->>A: three\n        end\n    end\n";

        // Act
        var scene = SceneQuery.Layout(Diagram);
        var items = SceneQuery.SequenceItems(scene);

        // Assert: the loop frame (top, right, bottom, left) around the alt frame, whose fifth line is the else separator
        Assert.Equal(
            [(54.0, 75.0, 296.0, 75.0), (296.0, 75.0, 296.0, 352.0), (54.0, 352.0, 296.0, 352.0), (54.0, 75.0, 54.0, 352.0)],
            FrameLines(items, "i7"));
        Assert.Equal(
            [
                (64.0, 164.0, 286.0, 164.0), (286.0, 164.0, 286.0, 342.0), (64.0, 342.0, 286.0, 342.0),
                (64.0, 164.0, 64.0, 342.0), (64.0, 258.0, 286.0, 258.0),
            ],
            FrameLines(items, "i6"));
        var section = ((SceneGroup)items.Find("frame", "i6")!).Children.OfType<SceneText>().Last();
        Assert.Equal(("[No]", 175.0), (section.Runs[0].Text, section.X));
        Assert.Equal(new SceneRect(-50, -10, 450, 458), scene.ViewBox);
    }

    /// <summary>
    ///     Validates the backgrounds of a rect and a rect nested in it, as Mermaid 11.17.2 draws them (the values are the
    ///     reference renderer's, which draws the same text): each background encloses its messages, the inner one inside
    ///     the outer one with Mermaid's box margins.
    /// </summary>
    [Fact]
    public void SequenceLayout_Draw_NestedRects_BackgroundsEncloseTheirMessages()
    {
        // Arrange
        const string Diagram =
            "sequenceDiagram\n    participant A\n    participant B\n    rect rgb(200, 220, 255)\n        A->>B: one\n" +
            "        rect rgb(255, 220, 200)\n            B->>A: two\n        end\n    end\n";

        // Act
        var scene = SceneQuery.Layout(Diagram);

        // Assert: the outer background, then the inner one, in scene order
        Assert.Equal(
            [new SceneRect(54, 75, 242, 138), new SceneRect(64, 139, 222, 64)],
            scene.Items.Where(item => item.Role == "rect").Select(item => Box(item)));
        Assert.Equal(new SceneRect(-50, -10, 450, 319), scene.ViewBox);
    }

    /// <summary>
    ///     Validates the boxes, as Mermaid 11.17.2 draws them (the values are the reference renderer's, which draws the
    ///     same text): each box around its participants with Mermaid's box margins and its title centered at its top, the
    ///     participants moved down for the titles, and the participants after a box moved right by its margin.
    /// </summary>
    [Fact]
    public void SequenceLayout_Draw_Boxes_EncloseTheirParticipantsBelowTheTitles()
    {
        // Arrange
        const string Diagram =
            "sequenceDiagram\n    box Aqua Front end\n        participant A\n        participant B\n    end\n" +
            "    box Pink\n        participant C\n    end\n    participant D\n    A->>B: one\n    B->>C: two\n    C->>D: three\n";

        // Act
        var scene = SceneQuery.Layout(Diagram);
        var items = SceneQuery.SequenceItems(scene);
        var boxes = scene.Items.Where(item => item.Role == "box").OfType<SceneGroup>().ToList();

        // Assert: the Pink box (no title) and the Front end box, in scene order
        Assert.Equal([new SceneRect(400, -5, 200, 334), new SceneRect(-20, -5, 400, 334)], boxes.Select(box => Box(box.Children[0])));
        Assert.Empty(boxes[0].Children.OfType<SceneText>());
        var title = boxes[1].Children.OfType<SceneText>().Single();
        Assert.Equal(("Front end", 180.0), (title.Runs[0].Text, title.X));

        // Assert: the participants below the titles and moved right after each box
        Assert.Equal([5.0, 205.0, 425.0, 640.0], new[] { "A", "B", "C", "D" }.Select(name => Box(items.Find("actor-top", name)).X));
        Assert.Equal(27, Box(items.Find("actor-top", "A")).Y);
        Assert.Equal(new SceneRect(-50, -10, 890, 340), scene.ViewBox);
    }

    /// <summary>
    ///     Validates that a box whose title is wider than its participants is widened, with the participants centered in
    ///     it, as Mermaid 11.17.2 draws it (the values are the reference renderer's, which draws the same text).
    /// </summary>
    [Fact]
    public void SequenceLayout_Draw_BoxTitleWiderThanParticipants_CentersThem()
    {
        // Arrange
        const string Diagram =
            "sequenceDiagram\n    box Aqua A box title that is much wider than its only participant\n        participant A\n    end\n" +
            "    participant B\n    A->>B: x\n";

        // Act
        var scene = SceneQuery.Layout(Diagram);
        var items = SceneQuery.SequenceItems(scene);
        var box = scene.Items.Where(item => item.Role == "box").OfType<SceneGroup>().Single();

        // Assert: the box as wide as its title, A centered in it, and B after it
        Assert.Equal(new SceneRect(-20, -5, 322, 246), Box(box.Children[0]));
        Assert.Equal(141.0, box.Children.OfType<SceneText>().Single().X);
        Assert.Equal(new SceneRect(66, 27, 150, 65), Box(items.Find("actor-top", "A")));
        Assert.Equal(342, Box(items.Find("actor-top", "B")).X);
        Assert.Equal(new SceneRect(-50, -10, 592, 252), scene.ViewBox);
    }

    /// <summary>
    ///     Validates that the hidden menus of participants with links grow the view box, as Mermaid 11.17.2 sizes it (the
    ///     values are the reference renderer's, which draws the same text): eight links make a panel 20 + 8 x 30 = 260
    ///     high below B, and a wide link label makes the panel wider than B, while the hidden menu draws nothing.
    /// </summary>
    [Fact]
    public void SequenceLayout_Draw_HiddenMenus_GrowTheViewBox()
    {
        // Arrange
        const string Head = "sequenceDiagram\n    participant A\n    participant B\n";
        var links = string.Concat(Enumerable.Range(1, 8).Select(i => $"    link B: Link {i} @ https://example.com/{i}\n"));

        // Act
        var plain = SceneQuery.Layout(Head + "    A->>B: x\n");
        var tall = SceneQuery.Layout(Head + links + "    A->>B: x\n");
        var wide = SceneQuery.Layout(
            Head + "    link B: A link label much wider than its participant @ https://example.com/docs\n    A->>B: x\n");

        // Assert: the view box grows for the tall and the wide menu, and the menu stays empty
        Assert.Equal(new SceneRect(-50, -10, 450, 215), plain.ViewBox);
        Assert.Equal(new SceneRect(-50, -10, 450, 336), tall.ViewBox);
        Assert.Equal(new SceneRect(-50, -10, 639, 215), wide.ViewBox);
        Assert.Empty(((SceneGroup)SceneQuery.SequenceItems(tall).Find("menu", "actor1_popup")!).Children);
    }

    /// <summary>
    ///     Validates that the forceMenus setting draws the menu panel below its participant, 20 + 2 x 30 high and as wide
    ///     as its widest link label, as Mermaid 11.17.2 draws it (the values are the reference renderer's, which draws the
    ///     same text).
    /// </summary>
    [Fact]
    public void SequenceLayout_Draw_ForceMenus_PanelBelowTheParticipant()
    {
        // Arrange
        const string Diagram =
            "---\nconfig:\n  sequence:\n    forceMenus: true\n---\nsequenceDiagram\n    participant A\n    participant B\n" +
            "    link B: Docs @ https://example.com/docs\n" +
            "    link B: A link label much wider than its participant @ https://example.com/wide\n    A->>B: x\n";

        // Act
        var scene = SceneQuery.Layout(Diagram);
        var menu = (SceneGroup)SceneQuery.SequenceItems(scene).Find("menu", "actor1_popup")!;

        // Assert: the panel below B, the link texts in it, and the view box holding it
        Assert.Equal(new SceneRect(200, 65, 339, 80), Box(menu.Children.OfType<ScenePath>().Single(child => child.Role == "menu-panel")));
        Assert.Equal(
            [(210.0, "Docs"), (210.0, "A link label much wider than its participant")],
            menu.Children.OfType<SceneText>().Select(text => (text.X, text.Runs[0].Text)));
        Assert.Equal(new SceneRect(-50, -10, 639, 215), scene.ViewBox);
    }

    /// <summary>
    ///     Gets the bounds of an item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The bounds.</returns>
    private static SceneRect Box(SceneItem? item) => Scene.BoundsOf([item!])!.Value;

    /// <summary>
    ///     Gets the line of a message.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <param name="id">The message id.</param>
    /// <returns>The line.</returns>
    private static ScenePath Line(SceneQuery.SequenceScene items, string id) =>
        (ScenePath)((SceneGroup)items.Find("message", id)!).Children.First(child => child.Role == "message-line");

    /// <summary>
    ///     Gets the bounds of the box of a note.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <param name="id">The note id.</param>
    /// <returns>The bounds.</returns>
    private static SceneRect NoteBox(SceneQuery.SequenceScene items, string id) =>
        Box(((SceneGroup)items.Find("note", id)!).Children[0]);

    /// <summary>
    ///     Gets the text of a note.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <param name="id">The note id.</param>
    /// <returns>The text.</returns>
    private static SceneText NoteText(SceneQuery.SequenceScene items, string id) =>
        ((SceneGroup)items.Find("note", id)!).Children.OfType<SceneText>().Single();

    /// <summary>
    ///     Gets the start and end of each line of a frame, in order.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <param name="id">The id of the end marker of the block.</param>
    /// <returns>The lines as (x1, y1, x2, y2).</returns>
    private static List<(double, double, double, double)> FrameLines(SceneQuery.SequenceScene items, string id) =>
        [
            .. ((SceneGroup)items.Find("frame", id)!).Children
                .Where(child => child.Role == "frame-line")
                .OfType<ScenePath>()
                .Select(path => (path.Segments[0].X, path.Segments[0].Y, path.Segments[1].X, path.Segments[1].Y)),
        ];
}
