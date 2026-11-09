using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Scenegraph;
using DemaConsulting.CanvasNet.Mermaid.Sequence;

namespace DemaConsulting.CanvasNet.Mermaid.Tests.Sequence;

/// <summary>
///     Unit tests of the SequenceRenderer class: the markers and strokes of every arrow, the stick figure, the paint order,
///     the paint of notes and activations, the central-connection circles, the sequence numbers and the text shown.
/// </summary>
public class SequenceRendererTests
{
    /// <summary>
    ///     Validates the markers of every arrow at the start and the end of the line, by Mermaid's marker names.
    /// </summary>
    /// <param name="arrow">The arrow.</param>
    /// <param name="start">The marker at the start, or empty.</param>
    /// <param name="end">The marker at the end, or empty.</param>
    [Theory]
    [InlineData("->", "", "")]
    [InlineData("-->", "", "")]
    [InlineData("->>", "", "arrowhead")]
    [InlineData("-->>", "", "arrowhead")]
    [InlineData("<<->>", "arrowhead", "arrowhead")]
    [InlineData("<<-->>", "arrowhead", "arrowhead")]
    [InlineData("-x", "", "crosshead")]
    [InlineData("--x", "", "crosshead")]
    [InlineData("-)", "", "filled-head")]
    [InlineData("--)", "", "filled-head")]
    [InlineData("-|\\", "", "solidTopArrowHead")]
    [InlineData("-|/", "", "solidBottomArrowHead")]
    [InlineData("-\\\\", "", "stickTopArrowHead")]
    [InlineData("-//", "", "stickBottomArrowHead")]
    [InlineData("--|\\", "", "solidTopArrowHead")]
    [InlineData("--//", "", "stickBottomArrowHead")]
    [InlineData("/|-", "solidBottomArrowHead", "")]
    [InlineData("\\|-", "solidTopArrowHead", "")]
    [InlineData("//-", "stickBottomArrowHead", "")]
    [InlineData("\\\\-", "stickTopArrowHead", "")]
    [InlineData("/|--", "solidBottomArrowHead", "")]
    [InlineData("\\\\--", "stickTopArrowHead", "")]
    public void SequenceRenderer_DrawMessage_Arrow_HasMermaidsMarkers(string arrow, string start, string end)
    {
        // Act
        var message = Message(SceneQuery.SequenceItems(SceneQuery.Layout($"sequenceDiagram\n    A{arrow}B: x\n")), "i0");

        // Assert
        Assert.Equal(start, message.Children.FirstOrDefault(child => child.Role == "marker-start")?.Id ?? string.Empty);
        Assert.Equal(end, message.Children.FirstOrDefault(child => child.Role == "marker-end")?.Id ?? string.Empty);
    }

    /// <summary>
    ///     Validates the strokes of the message lines: 1.5 wide in the signal color, and dashed 3, 3 when dotted.
    /// </summary>
    [Fact]
    public void SequenceRenderer_DrawMessage_Lines_AreSolidOrDashed()
    {
        // Act
        var items = SceneQuery.SequenceItems(SceneQuery.Layout("sequenceDiagram\n    A->>B: solid\n    B-->>A: dotted\n"));
        var solid = Line(Message(items, "i0"));
        var dotted = Line(Message(items, "i1"));

        // Assert
        Assert.Equal(new SceneStroke(new Rgba32(0x33, 0x33, 0x33, 255), 1.5), solid.Stroke);
        Assert.Equal([3.0, 3.0], dotted.Stroke!.Dash);
        Assert.Equal(1.5, dotted.Stroke.Width);
    }

    /// <summary>
    ///     Validates that the arrowhead is placed with its reference point at the end of the line, pointing along it, and
    ///     that a marker scaled by the stroke width (the filled head) grows with it.
    /// </summary>
    [Fact]
    public void SequenceRenderer_DrawMessage_Markers_FollowTheLine()
    {
        // Act
        var items = SceneQuery.SequenceItems(SceneQuery.Layout("sequenceDiagram\n    A->>B: right\n    B->>A: left\n    A-)B: async\n"));
        var right = Bounds(Marker(Message(items, "i0"), "marker-end"));
        var left = Bounds(Marker(Message(items, "i1"), "marker-end"));
        var filled = Bounds(Marker(Message(items, "i2"), "marker-end"));

        // Assert: the tip past the end of the line by 10 - 7.9 and its stroke; mirrored for the left arrow; the filled
        // head 9 units wide scaled by 1.5
        var rightEnd = Line(Message(items, "i0")).Segments[^1].X;
        var leftEnd = Line(Message(items, "i1")).Segments[^1].X;
        Assert.InRange(right.Right - rightEnd, 2.1, 3.5);
        Assert.InRange(right.Height, 10, 11.5);
        Assert.InRange(leftEnd - left.X, 2.1, 3.5);
        Assert.Equal(9 * 1.5, filled.Width, 3);
    }

    /// <summary>
    ///     Validates the stick figure of an <c>actor</c>: four limbs and a head of radius 15, 65 high, with the label below.
    /// </summary>
    [Fact]
    public void SequenceRenderer_DrawActor_Actor_IsAStickFigure()
    {
        // Act
        var figure = (SceneGroup)SceneQuery.SequenceItems(SceneQuery.Layout("sequenceDiagram\n    actor A\n    A->>A: x\n")).Find("actor-top", "A")!;

        // Assert
        Assert.Equal(4, figure.Children.Count(child => child.Role == "actor-line"));
        var head = Bounds(figure.Children.Single(child => child.Role == "actor-head"));
        Assert.Equal(new SceneRect(60, -5, 30, 30), head);
        var shape = Scene.BoundsOf([.. figure.Children.OfType<ScenePath>()])!.Value;
        Assert.Equal(65, shape.Height, 6);
        Assert.Single(figure.Children.OfType<SceneText>());
    }

    /// <summary>
    ///     Validates Mermaid's paint order: the mirrored participants, then each participant's lifeline and box, lowered
    ///     to the back in reverse order, then the notes, then the messages.
    /// </summary>
    [Fact]
    public void SequenceRenderer_Items_PaintOrder_IsMermaids()
    {
        // Act
        var items = SceneQuery.SequenceItems(SceneQuery.Layout("sequenceDiagram\n    A->>B: x\n    Note over A: n\n"));

        // Assert
        Assert.Equal(
            ["actor-bottom:B", "actor-bottom:A", "lifeline:B", "actor-top:B", "lifeline:A", "actor-top:A", "note:i1", "message:i0"],
            items.Order);
    }

    /// <summary>
    ///     Validates the paint of notes and activations: the theme's note and activation colors over Mermaid's attributes.
    /// </summary>
    [Fact]
    public void SequenceRenderer_DrawNoteAndActivation_UseTheThemeColors()
    {
        // Act
        var items = SceneQuery.SequenceItems(SceneQuery.Layout("sequenceDiagram\n    A->>+B: x\n    Note over A: n\n    B-->>-A: y\n"));
        var note = (ScenePath)((SceneGroup)items.Find("note", "i2")!).Children[0];
        var activation = (ScenePath)items.WithRole("activation").Single();

        // Assert
        Assert.Equal(new Rgba32(0xFF, 0xF5, 0xAD, 255), note.Fill);
        Assert.Equal(new Rgba32(0xAA, 0xAA, 0x33, 255), note.Stroke!.Color);
        Assert.Equal("activation0", activation.Id);
        Assert.Equal(new Rgba32(0xF4, 0xF4, 0xF4, 255), activation.Fill);
        Assert.Equal(new Rgba32(0x66, 0x66, 0x66, 255), activation.Stroke!.Color);
    }

    /// <summary>
    ///     Validates the central-connection circles: radius 5 on the lifelines they name, filled with the text color.
    /// </summary>
    [Fact]
    public void SequenceRenderer_DrawMessage_CentralConnection_DrawsCircles()
    {
        // Act
        var items = SceneQuery.SequenceItems(SceneQuery.Layout("sequenceDiagram\n    A()->>()B: both\n"));
        var circles = ((SceneGroup)items.Find("connection-circles", "i0")!).Children;

        // Assert
        Assert.Equal([new SceneRect(70, 104, 10, 10), new SceneRect(270, 104, 10, 10)], circles.Select(Bounds));
        Assert.All(circles, circle => Assert.Equal(new Rgba32(0x33, 0x33, 0x33, 255), ((ScenePath)circle).Fill));
    }

    /// <summary>
    ///     Validates a sequence number: a circle of radius 12 (the marker scaled by the 2-pixel line width) at the
    ///     sender's side, and the number in white, smaller for longer numbers.
    /// </summary>
    [Fact]
    public void SequenceRenderer_DrawMessage_SequenceNumber_IsACircleWithTheNumber()
    {
        // Act
        var items = SceneQuery.SequenceItems(SceneQuery.Layout("sequenceDiagram\n    autonumber 1000\n    A->>B: x\n"));
        var number = (SceneGroup)items.Find("sequence-number", "i1")!;
        var text = (SceneText)number.Children[1];

        // Assert
        Assert.Equal(new SceneRect(63, 97, 24, 24), Bounds(number.Children[0]));
        Assert.Equal(("1000", 9.0), (text.Runs[0].Text, text.Runs[0].Font.Size));
        Assert.Equal(new Rgba32(255, 255, 255, 255), text.Runs[0].Color);
    }

    /// <summary>
    ///     Validates the text shown for a text of the model: Mermaid's entity placeholders become their characters.
    /// </summary>
    [Fact]
    public void SequenceRenderer_DisplayText_DecodesEntityPlaceholders()
    {
        // Act / Assert
        Assert.Equal("I \u2665 it", SequenceRenderer.DisplayText("I \ufb02\u00b0\u00b09829\u00b6\u00df it"));
        Assert.Equal("a & b", SequenceRenderer.DisplayText("a \ufb02\u00b0amp\u00b6\u00df b"));
        Assert.Equal("plain", SequenceRenderer.DisplayText("plain"));
    }

    /// <summary>
    ///     Validates the geometry helpers: a rounded rectangle's box, and a circle's box.
    /// </summary>
    [Fact]
    public void SequenceRenderer_Segments_HaveTheirBoxes()
    {
        // Act
        var rect = new ScenePath(SequenceRenderer.RectSegments(1, 2, 30, 40, 3, 3)) { Role = "r" };
        var circle = new ScenePath(SequenceRenderer.CircleSegments(10, 20, 5)) { Role = "c" };

        // Assert
        Assert.Equal(new SceneRect(1, 2, 30, 40), Bounds(rect));
        Assert.Equal(new SceneRect(5, 15, 10, 10), Bounds(circle));
    }

    /// <summary>
    ///     Gets the group of a message.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <param name="id">The message id.</param>
    /// <returns>The group.</returns>
    private static SceneGroup Message(SceneQuery.SequenceScene items, string id) => (SceneGroup)items.Find("message", id)!;

    /// <summary>
    ///     Gets the line of a message group.
    /// </summary>
    /// <param name="message">The group.</param>
    /// <returns>The line.</returns>
    private static ScenePath Line(SceneGroup message) => (ScenePath)message.Children.First(child => child.Role == "message-line");

    /// <summary>
    ///     Gets a marker of a message group.
    /// </summary>
    /// <param name="message">The group.</param>
    /// <param name="role">The marker's role.</param>
    /// <returns>The marker.</returns>
    private static ScenePath Marker(SceneGroup message, string role) => (ScenePath)message.Children.First(child => child.Role == role);

    /// <summary>
    ///     Gets the bounds of an item, rounded to thousandths.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The bounds.</returns>
    private static SceneRect Bounds(SceneItem item)
    {
        var box = Scene.BoundsOf([item])!.Value;
        return new SceneRect(Math.Round(box.X, 3), Math.Round(box.Y, 3), Math.Round(box.Width, 3), Math.Round(box.Height, 3));
    }
}
