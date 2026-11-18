using DemaConsulting.CanvasNet.PlantUml.Sequence;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Sequence;

/// <summary>
///     Unit tests for the <c>SequenceLayout</c> unit: the participant columns, the rows, activations, notes, groups,
///     boxes, dividers, delays, the foot box, and the layout limits. The expected numbers are PlantUML's.
/// </summary>
public class SequenceLayoutTests
{
    /// <summary>
    ///     Validates the columns: the first participant at the margin and a message that pushes its receiver until the
    ///     arrow is as long as its text plus 24 pixels.
    /// </summary>
    [Fact]
    public void PlantUmlSequenceLayout_Columns_MessagePushesReceiver()
    {
        // Act
        var (_, geometry) = SequenceTestHelpers.Layout("participant A\nparticipant B\nA -> B : hello");

        // Assert
        var a = geometry.Participants[0];
        var b = geometry.Participants[1];
        Assert.Equal(10, a.PosB + geometry.OffsetX, 3);
        Assert.Equal(21.669, a.PosC + geometry.OffsetX, 3);
        Assert.Equal(27.466 + 24, b.PosC - a.PosC, 3);
    }

    /// <summary>
    ///     Validates the rows: heads aligned at the bottom, the first arrow 8 pixels below the life line start plus its
    ///     text, one row per message, the life line end 10 pixels below the last row, and the height with the foot box.
    /// </summary>
    [Fact]
    public void PlantUmlSequenceLayout_Rows_StackMessagesBelowHeads()
    {
        // Act
        var (_, geometry) = SequenceTestHelpers.Layout("A -> B : one\nB -> A : two");

        // Assert
        var messages = geometry.Items.OfType<PlacedMessage>().ToList();
        Assert.Equal(42.609, geometry.Participants[0].HeadBottom, 3);
        Assert.Equal(72.961, messages[0].ArrowY, 3);
        Assert.Equal(30.352, messages[1].ArrowY - messages[0].ArrowY, 3);
        Assert.Equal(121.313, geometry.Participants[0].TailTop, 2);
        Assert.Equal(121.313 + 32.609 + 10, geometry.Height, 2);
        Assert.True(messages[1].ToLeft);
    }

    /// <summary>
    ///     Validates the activations: a box 10 pixels wide centered on the life line from the arrow that starts it,
    ///     arrows that meet the box, and a nested box 5 pixels to the right.
    /// </summary>
    [Fact]
    public void PlantUmlSequenceLayout_Activations_BoxesAndArrowEnds()
    {
        // Act
        var (_, geometry) = SequenceTestHelpers.Layout("A -> B ++ : x\nB -> C : y\nB -> B ++ : z\nB -> A -- : w");

        // Assert
        var b = geometry.Participants[1];
        var messages = geometry.Items.OfType<PlacedMessage>().ToList();
        var boxes = geometry.Activations.Where(a => a.Participant == "B").OrderBy(a => a.X).ToList();
        Assert.Equal(2, boxes.Count);
        Assert.Equal(b.PosC - 5, boxes[0].X, 3);
        Assert.Equal(b.PosC, boxes[1].X, 3);
        Assert.Equal(messages[0].ArrowY, boxes[0].Y1, 3);
        Assert.Equal(b.PosC - 5, messages[0].XRight, 3);
        Assert.Equal(b.PosC + 5, messages[1].XLeft, 3);
        Assert.Equal(b.PosC, messages[2].XRight, 3);
        Assert.Equal(messages[2].ArrowY, boxes[1].Y2, 3);
    }

    /// <summary>
    ///     Validates a self message: the loop leaves the life line and returns 13 pixels lower, and the next participant
    ///     clears the wider of the loop and the text.
    /// </summary>
    [Fact]
    public void PlantUmlSequenceLayout_SelfMessage_PushesNextParticipant()
    {
        // Act
        var (_, geometry) = SequenceTestHelpers.Layout("participant A\nparticipant B\nA -> A : x\nA -> A : a much longer label");

        // Assert
        var loops = geometry.Items.OfType<PlacedSelf>().ToList();
        var a = geometry.Participants[0];
        Assert.Equal(13, loops[0].BottomY - loops[0].TopY, 3);
        Assert.Equal(a.PosC, loops[0].StartX, 3);
        Assert.Equal(a.PosC + 7, loops[0].TextX, 3);
        Assert.True(geometry.Participants[1].PosC - a.PosC >= loops[1].Label.Width + 14 - 1e-6);
    }

    /// <summary>
    ///     Validates the notes: the outline 5 pixels inside its layout box, a left note that moves the diagram right, a
    ///     note across participants as wide as their columns, and a note on a message's row.
    /// </summary>
    [Fact]
    public void PlantUmlSequenceLayout_Notes_PlacedBesideOverAndOnRows()
    {
        // Act
        var (_, geometry) = SequenceTestHelpers.Layout("A -> B : m\nnote right : r\nnote left of A : a long note on the left\nnote over A, B : x");

        // Assert
        var notes = geometry.Items.OfType<PlacedNote>().ToList();
        var message = geometry.Items.OfType<PlacedMessage>().Single();
        var (a, b) = (geometry.Participants[0], geometry.Participants[1]);
        Assert.Equal(b.PosC + 5, notes[0].X, 3);
        Assert.Equal(message.ArrowY - 22.352 + 5, notes[0].Y, 3);
        Assert.Equal(10 + 5, notes[1].X + geometry.OffsetX, 3);
        Assert.Equal(Math.Floor(b.PosB + b.Width - a.PosB - 10), notes[2].Width);
        Assert.Equal(a.PosB + 5, notes[2].X, 3);
    }

    /// <summary>
    ///     Validates a group frame: 16 pixels outside its messages, its header tab, its sections, and the next
    ///     participant pushed past a wide header.
    /// </summary>
    [Fact]
    public void PlantUmlSequenceLayout_Groups_FrameAroundContent()
    {
        // Act
        var (_, geometry) = SequenceTestHelpers.Layout(
            "participant A\nparticipant B\nparticipant C\nalt a long condition that is wider than the messages\nA -> B : m\nelse other\nA -> B : n\nend");

        // Assert
        var group = geometry.Items.OfType<PlacedGroup>().Single();
        var (a, c) = (geometry.Participants[0], geometry.Participants[2]);
        Assert.Equal(a.PosC - 16, group.X, 3);
        Assert.Equal(group.Keyword.Width + 45, group.TabWidth, 3);
        Assert.Single(group.Sections);
        Assert.True(c.PosC >= group.X + group.Width + 9 - 1e-6);
    }

    /// <summary>
    ///     Validates a box: 4 pixels around its participants (widened for a wide title), the participants after it 20
    ///     pixels away, and the heads below its title.
    /// </summary>
    [Fact]
    public void PlantUmlSequenceLayout_Boxes_AroundParticipants()
    {
        // Act
        var (_, geometry) = SequenceTestHelpers.Layout("box \"A rather wide box title\"\nparticipant A\nend box\nparticipant B\nA -> B");

        // Assert
        var box = Assert.Single(geometry.Boxes);
        var (a, b) = (geometry.Participants[0], geometry.Participants[1]);
        Assert.True(box.X < a.PosB - 4);
        Assert.True(box.Width >= box.Title!.Width);
        Assert.Equal(20, b.PosB - (box.X + box.Width - 4), 3);
        Assert.Equal(10 + 1 + box.Title.Height + 4 + a.PrefHeight, a.HeadBottom, 3);
    }

    /// <summary>
    ///     Validates dividers across the whole diagram, delays with dotted life lines, and spaces (after a message row of 14 pixels, the height of a message without a label).
    /// </summary>
    [Fact]
    public void PlantUmlSequenceLayout_Separators_DividersDelaysAndSpaces()
    {
        // Act
        var (_, geometry) = SequenceTestHelpers.Layout("A -> B\n== a very long divider text here ==\nA -> B\n... later ...\nA -> B\n||40||\nA -> B");

        // Assert
        var divider = geometry.Items.OfType<PlacedDivider>().Single();
        Assert.Equal(10, divider.X1 + geometry.OffsetX, 3);
        Assert.Equal(geometry.Width - 10, divider.X2 + geometry.OffsetX, 3);
        Assert.Equal(2, geometry.Lifelines.Count(l => l.Delay));
        Assert.Single(geometry.Items.OfType<PlacedDelay>());
        var arrows = geometry.Items.OfType<PlacedMessage>().Select(m => m.ArrowY).ToList();
        Assert.Equal(14 + 40, arrows[3] - arrows[2], 3);
    }

    /// <summary>
    ///     Validates that a hidden foot box draws no tails and ends the diagram 10 pixels below the life lines, and that
    ///     a destroyed life line ends with its row.
    /// </summary>
    [Fact]
    public void PlantUmlSequenceLayout_HiddenFootboxAndDestroy()
    {
        // Act
        var (_, geometry) = SequenceTestHelpers.Layout("hide footbox\nA -> B : x\nA -> B !! : y\nA -> A : z");

        // Assert
        Assert.All(geometry.Participants, p => Assert.True(double.IsNaN(p.TailTop)));
        var destroy = geometry.Items.OfType<PlacedDestroy>().Single();
        var lifelineB = geometry.Lifelines.Where(l => l.Participant == "B").Max(l => l.Y2);
        var lifelineA = geometry.Lifelines.Where(l => l.Participant == "A").Max(l => l.Y2);
        Assert.Equal(destroy.Y + 8, lifelineB, 3);
        Assert.Equal(lifelineA + 10, geometry.Height, 3);
    }

    /// <summary>
    ///     Validates that a layout over the work limit fails located with plantuml-limit-exceeded, and that a canceled
    ///     layout stops.
    /// </summary>
    [Fact]
    public void PlantUmlSequenceLayout_Limits_WorkAndCancellation()
    {
        // Arrange
        var model = SequenceTestHelpers.Parse(string.Join('\n', Enumerable.Range(0, 300).Select(i => $"P{i % 20} -> P{(i + 1) % 20} : m{i}")));
        var tight = new PlantUmlRenderOptions { Limits = new PlantUmlLimits { MaxLayoutWork = 100 } };
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();

        // Act
        var ex = Assert.Throws<PlantUmlRenderException>(() => SequenceLayout.Run(model, SequenceTestHelpers.Context(tight)));
        Assert.ThrowsAny<OperationCanceledException>(() => SequenceLayout.Run(model, SequenceTestHelpers.Canceling(null, canceled.Token)));

        // Assert
        Assert.Equal(PlantUmlDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Equal(2, ex.Location.Line);
    }
}
