using DemaConsulting.CanvasNet.Mermaid.Scenegraph;
using DemaConsulting.CanvasNet.Mermaid.Sequence;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Sequence;

/// <summary>
///     Unit tests of the SequenceLayout class: the participant margins, the vertical placement of messages, the view box,
///     the title, the mirrored participants, hidden participants, created and destroyed participants, activations, and the
///     work limit.
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
}
