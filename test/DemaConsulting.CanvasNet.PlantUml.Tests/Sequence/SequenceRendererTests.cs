using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Sequence;
using DemaConsulting.CanvasNet.PlantUml.Styling;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Sequence;

/// <summary>
///     Unit tests for the <c>SequenceRenderer</c> unit: the scene items of participants, life lines, activations,
///     messages and their heads, notes, groups, dividers, boxes, and destroyed life lines.
/// </summary>
public class SequenceRendererTests
{
    /// <summary>
    ///     Validates that every participant kind draws its head and tail with the theme's colors and its id.
    /// </summary>
    [Fact]
    public void PlantUmlSequenceRenderer_Participants_DrawEveryKind()
    {
        // Act
        var scene = SequenceTestHelpers.Draw("actor A\nboundary B\ncontrol C\nentity E\ndatabase D\ncollections L\nqueue Q\nparticipant P\nA -> P");

        // Assert
        foreach (var code in new[] { "A", "B", "C", "E", "D", "L", "Q", "P" })
        {
            Assert.NotEmpty(SequenceTestHelpers.Bounds(scene, "participant-head", code));
            Assert.NotEmpty(SequenceTestHelpers.Bounds(scene, "participant-tail", code));
            Assert.Contains(scene.Items.OfType<SceneText>(), t => t.Role == "participant-head" && t.Id == code);
        }

        var participant = scene.Items.OfType<ScenePath>().First(p => p.Role == "participant-head" && p.Id == "P");
        Assert.Equal(SkinTheme.GreyBlue, participant.Fill);
        Assert.Equal(SkinTheme.LineColor, participant.Stroke!.Color);
        Assert.Equal(0.5, participant.Stroke.Width);
    }

    /// <summary>
    ///     Validates the life lines (dashed 5,5, and dotted in a delay) and the activation boxes (white, outlined).
    /// </summary>
    [Fact]
    public void PlantUmlSequenceRenderer_LifelinesAndActivations()
    {
        // Act
        var scene = SequenceTestHelpers.Draw("A -> B ++ : x\n...\nB --> A -- : y");

        // Assert
        var lines = scene.Items.OfType<ScenePath>().Where(p => p.Role == "lifeline").ToList();
        Assert.Contains(lines, l => l.Stroke!.Dash is [5, 5]);
        Assert.Contains(lines, l => l.Stroke!.Dash is [1, 4]);
        var activation = scene.Items.OfType<ScenePath>().Single(p => p.Role == "activation");
        Assert.Equal(SkinTheme.White, activation.Fill);
        Assert.Equal(10, Scene.BoundsOf([activation])!.Value.Width, 3);
    }

    /// <summary>
    ///     Validates the message heads: filled, open, half, cross and circle, on either end, and the dotted body.
    /// </summary>
    [Fact]
    public void PlantUmlSequenceRenderer_Messages_DrawHeadsAndBodies()
    {
        // Act
        var scene = SequenceTestHelpers.Draw("A -> B\nA ->> B\nA -\\ B\nA ->x B\nA ->o B\nA <-> B\nB --> A");

        // Assert
        var heads = scene.Items.OfType<ScenePath>().Where(p => p.Role == "message-head").ToList();
        Assert.Equal(9, heads.Count);
        Assert.Equal(2, heads.Count(h => h.Fill is null));
        var bodies = scene.Items.OfType<ScenePath>().Where(p => p.Role == "message").ToList();
        Assert.Equal(7, bodies.Count);
        Assert.Equal([2, 2], bodies[^1].Stroke!.Dash);
        Assert.All(bodies.Take(6), b => Assert.Null(b.Stroke!.Dash));
    }

    /// <summary>
    ///     Validates that a thick arrow draws its line and its open head with the arrow's width, while its filled and half
    ///     heads keep a 1 pixel outline, as in PlantUML, so that the heads do not grow over the labels.
    /// </summary>
    [Fact]
    public void PlantUmlSequenceRenderer_ThickArrow_HeadsKeepAThinOutline()
    {
        // Act
        var scene = SequenceTestHelpers.Draw("skinparam sequenceArrowThickness 4\nA -> B\nA -\\ B\nA -/ B\nA ->> B");

        // Assert
        var heads = scene.Items.OfType<ScenePath>().Where(p => p.Role == "message-head").ToList();
        Assert.Equal([1.0, 1.0, 1.0, 4.0], heads.Select(h => h.Stroke!.Width));
        Assert.Equal([true, true, true, false], heads.Select(h => h.Fill is not null));
        var bodies = scene.Items.OfType<ScenePath>().Where(p => p.Role == "message").ToList();
        Assert.All(bodies, b => Assert.Equal(4, b.Stroke!.Width));
    }

    /// <summary>
    ///     Validates a self message: three lines and a head pointing back to the life line, and a numbered label.
    /// </summary>
    [Fact]
    public void PlantUmlSequenceRenderer_SelfMessage_LoopNumberAndLabel()
    {
        // Act
        var scene = SequenceTestHelpers.Draw("autonumber\nA -> A : loop");

        // Assert
        Assert.Equal(3, SequenceTestHelpers.Bounds(scene, "message").Count);
        Assert.Single(SequenceTestHelpers.Bounds(scene, "message-head"));
        Assert.Equal("1", Assert.Single(SequenceTestHelpers.Texts(scene, "message-number")).Text);
        Assert.Equal("loop", Assert.Single(SequenceTestHelpers.Texts(scene, "message-text")).Text);
        Assert.True(SequenceTestHelpers.Texts(scene, "message-number")[0].Runs[0].Font.Bold);
    }

    /// <summary>
    ///     Validates the notes: a sheet with a folded corner, a hexagon and a rectangle, with the note color.
    /// </summary>
    [Fact]
    public void PlantUmlSequenceRenderer_Notes_DrawShapes()
    {
        // Act
        var scene = SequenceTestHelpers.Draw("A -> B\nnote over A : n\nhnote over A : h\nrnote over A : r");

        // Assert
        var notes = scene.Items.OfType<ScenePath>().Where(p => p.Role == "note").ToList();
        Assert.Equal(4, notes.Count);
        Assert.All(notes, n => Assert.Equal(SkinTheme.NoteBackground, n.Fill));
        Assert.Equal(7, notes[2].Segments.Count);
        Assert.Equal(3, SequenceTestHelpers.Texts(scene, "note-text").Count);
    }

    /// <summary>
    ///     Validates a group: its header tab, its frame, its texts, and a dashed section line.
    /// </summary>
    [Fact]
    public void PlantUmlSequenceRenderer_Groups_DrawTabFrameAndSections()
    {
        // Act
        var scene = SequenceTestHelpers.Draw("alt #LightYellow yes\nA -> B\nelse no\nA -> B\nend");

        // Assert
        Assert.Single(SequenceTestHelpers.Bounds(scene, "group-header"));
        Assert.Single(SequenceTestHelpers.Bounds(scene, "group"));
        Assert.Single(SequenceTestHelpers.Bounds(scene, "group-fill"));
        var section = scene.Items.OfType<ScenePath>().Single(p => p.Role == "group-else");
        Assert.Equal([2, 2], section.Stroke!.Dash);
        Assert.Equal(["alt", "[yes]", "[no]"], SequenceTestHelpers.Texts(scene, "group-text").Select(t => t.Text));
    }

    /// <summary>
    ///     Validates a divider (with and without text), a box with its title, a delay text, and the cross of a destroyed
    ///     life line in PlantUML's color.
    /// </summary>
    [Fact]
    public void PlantUmlSequenceRenderer_DividersBoxesDelaysAndDestroy()
    {
        // Act
        var scene = SequenceTestHelpers.Draw("box Team\nparticipant A\nend box\nA -> B\n== Phase ==\n====\n... wait ...\nA -> B !!");

        // Assert
        Assert.Equal(5, SequenceTestHelpers.Bounds(scene, "divider").Count);
        Assert.Equal("Phase", Assert.Single(SequenceTestHelpers.Texts(scene, "divider-text")).Text);
        Assert.Single(SequenceTestHelpers.Bounds(scene, "box"));
        Assert.Equal("Team", Assert.Single(SequenceTestHelpers.Texts(scene, "box-text")).Text);
        Assert.Equal("wait", Assert.Single(SequenceTestHelpers.Texts(scene, "delay-text")).Text);
        var destroy = scene.Items.OfType<ScenePath>().Single(p => p.Role == "destroy");
        Assert.Equal(new Canvas.Rgba32(0xA8, 0x00, 0x36, 255), destroy.Stroke!.Color);
    }

    /// <summary>
    ///     Validates that the scene is moved by the offset of the layout and that its view box is the body size.
    /// </summary>
    [Fact]
    public void PlantUmlSequenceRenderer_Render_MovesByOffsetAndSizesViewBox()
    {
        // Arrange
        var (model, geometry) = SequenceTestHelpers.Layout("A -> B\nnote left of A : a note that sticks out on the left");

        // Act
        var scene = SequenceRenderer.Render(model, geometry);

        // Assert
        Assert.True(geometry.OffsetX > 0);
        Assert.Equal(new SceneRect(0, 0, geometry.Width, geometry.Height), scene.ViewBox);
        var head = SequenceTestHelpers.Bounds(scene, "participant-head", "A")[0];
        Assert.Equal(geometry.Participants[0].PosB + geometry.OffsetX, head.X, 3);
        Assert.Equal(15, SequenceTestHelpers.Bounds(scene, "note")[0].X, 3);
    }
}
